/// Test declarations and their assertion statements, read from JavaScript/TypeScript (Playwright, Mocha-style) and
/// F# test source. Shared by `verify-defect-closures` (which tests are declared and run) and `assertion-ratchet`
/// (which assertions each test holds).
///
/// This is a lexical reading, not a parser: comments (line and block) are removed, string, template and regex
/// literal contents are masked so their brackets do not count, and a test block runs from its declaration to the
/// close of the declaration's call (JavaScript) or to the end of its offside indentation (F#). Statements are whole
/// statements: an assertion's continuation lines (open brackets, a leading `.`, a trailing operator, an `if`/`for`
/// head without braces) are joined, so a `.not` added on a continuation line, or an `if (false)` put in front of an
/// assertion, changes the statement.
///
/// The JavaScript/TypeScript scanner is a heuristic lexer, not a parser. It decides what a `/` is from the token
/// before it (a `)` closing a statement head, a `}` closing a block, an operator: a regular expression; an operand,
/// a `}` closing an object literal: division) and fails closed (UNPARSEABLE) where it cannot tell or its reading is
/// inconsistent. An adversarially written file may still desynchronise it in a way it does not detect, so the
/// independent review must inspect the test files it relies on. JSX/TSX files are not read at all (unsupported).
module TestBlocks

open System
open System.IO
open System.Text.RegularExpressions

type Language =
    | Js
    | FSharp

let languageOf (path: string) =
    match Path.GetExtension(path).ToLowerInvariant() with
    | ".js"
    | ".mjs"
    | ".cjs"
    | ".ts"
    | ".mts"
    | ".cts" -> Some Js
    | ".fs"
    | ".fsx" -> Some FSharp
    | _ -> None

/// JSX/TSX: markup with its own quoting (`<p>Don't</p>`), which this lexer does not read. Such a file is reported as
/// unsupported, never read as JavaScript.
let isUnsupported (path: string) =
    match Path.GetExtension(path).ToLowerInvariant() with
    | ".jsx"
    | ".tsx" -> true
    | _ -> false

/// A file a test runner collects tests from (a Playwright/Mocha spec, an F# test module).
let isSpec (path: string) =
    Regex.IsMatch(path, @"(\.(spec|test)\.[cm]?[jt]sx?|Tests?\.fsx?)$")

type Kind =
    | Test
    | Describe
    /// Everything of a file outside its test blocks: helpers, fixtures, module-level code.
    | Module

type Block =
    { Title: string
      Kind: Kind
      /// Declared with, or containing, a skip/fixme/fail/only marker (or inside a skipped block, or in a file with an
      /// `.only`): what it asserts does not run as a held check. A body marker is any `.skip(`/`.fixme(`/`.fail(`
      /// call anywhere in the block, its declaration line included (`test.skip()`, `testInfo.skip()`,
      /// `test.info().skip()`), or a call of a name the file binds to one (`const { skip } = test; skip();`).
      /// Markers reached any other way (passed as an argument, wrapped in a helper defined in another file, read
      /// through a computed property) are not seen: an inherent limit of a lexical reading.
      Skipped: bool
      /// Skipped only by conditional skip calls (`test.skip(cond, ...)`): it may run.
      SkipConditional: bool
      /// Every statement of the block, comment-free and whitespace-collapsed, in order.
      Statements: string list
      /// The same statements with string, template and regex literal contents masked (`_`): what the code does, not
      /// what it quotes.
      Masked: string list }

type private Mode =
    | Code
    | LineComment
    | BlockComment of depth: int
    | Quoted of quote: char
    | Verbatim
    | Triple
    | Template
    | TemplateExpr of depth: int
    | RegexLit of inClass: bool
    | Backticks

/// A literal context: its contents are masked.
let private isLiteral =
    function
    | Quoted _
    | Verbatim
    | Triple
    | Template
    | TemplateExpr _
    | RegexLit _
    | Backticks -> true
    | Code
    | LineComment
    | BlockComment _ -> false

let private describeMode =
    function
    | Code -> "code"
    | LineComment -> "a line comment"
    | BlockComment _ -> "a block comment"
    | Quoted q -> sprintf "a %c-quoted string" q
    | Verbatim -> "a verbatim string"
    | Triple -> "a triple-quoted string"
    | Template -> "a template literal"
    | TemplateExpr _ -> "a template literal's ${...}"
    | RegexLit _ -> "a regular expression literal"
    | Backticks -> "a ``...`` identifier"

/// Words after which a JavaScript `/` begins a regular expression, not a division.
let private jsRegexKeywords = set [ "return"; "typeof"; "case"; "do"; "else"; "in"; "of"; "new"; "delete"; "void"; "throw"; "instanceof"; "yield"; "await" ]

/// Statement heads whose closing `)` may be followed by a regular expression (`if (x) /re/.test(s)`).
let private jsHeadKeywords = set [ "if"; "while"; "for"; "with"; "catch"; "switch" ]

/// Words after which a JavaScript `{` opens a block.
let private jsBlockKeywords = set [ "else"; "try"; "finally"; "do" ]

/// What a JavaScript `{` opened, which decides what a `/` after its `}` is: after an expression (an object literal)
/// it is division, after a block a regular expression may follow. Where the reading cannot tell (after a `)` that is
/// not a statement head: a function declaration or expression, a method; after an identifier: a class body; after a
/// `:`: an object value or a label), the brace is Ambiguous, and a `/` after its `}` makes the file UNPARSEABLE.
type private Brace =
    | ExprBrace
    | BlockBrace
    | AmbiguousBrace

// Previous-character sentinels: after the `)` of a statement head; after the `}` of an ambiguous brace.
let private afterHead = '\001'
let private afterAmbiguous = '\002'

/// The scanner's state: the stack of open contexts (the innermost first; Code at the bottom), the JavaScript
/// parentheses open (each: whether it opened a statement head), and the previous significant code character.
type private ScanState = { Stack: Mode list; Parens: bool list; Braces: Brace list; Prev: char; Problem: string option }

/// The text with comments blanked (`code`) and the same text with literal contents blanked as well (`mask`). Both keep
/// the length and every newline, so line i of one is line i of the other. Strings, templates and comments inside a
/// template's `${...}` are read as code is, recursively. The third value is why the text could not be read
/// consistently, if it could not: it ends inside a literal or comment, a JavaScript string or regular expression runs
/// past its line, or the parentheses do not balance. A reading with a problem is not to be trusted at all (fail
/// closed): an early mistake hides everything after it.
let scan (lang: Language) (text: string) : string * string * string option =
    let n = text.Length
    let at i = if i < n then text.[i] else '\000'
    let regexMayFollow (prev: char) = prev = '\000' || prev = afterHead || "(,=:[!&|?{};+-*%<>~^".IndexOf prev >= 0

    /// Whether the code just before position i (whitespace skipped) ends with `=>`.
    let arrowBefore i =
        let rec skipSpace j = if j >= 0 && Char.IsWhiteSpace text.[j] then skipSpace (j - 1) else j
        let j = skipSpace (i - 1)
        j >= 1 && text.[j] = '>' && text.[j - 1] = '='
    let blank c = if c = '\n' then '\n' else ' '
    let lit c = if c = '\n' then '\n' else '_'

    /// The identifier that ends just before position i (whitespace skipped), or "".
    let wordBefore i =
        let rec skipSpace j = if j >= 0 && Char.IsWhiteSpace text.[j] then skipSpace (j - 1) else j
        let stop = skipSpace (i - 1)
        let rec start j = if j >= 0 && (Char.IsLetterOrDigit text.[j] || text.[j] = '_' || text.[j] = '$') then start (j - 1) else j
        let first = start stop
        if stop < 0 || first = stop then "" else text.Substring(first + 1, stop - first)

    let fail (s: ScanState) why = { s with Problem = s.Problem |> Option.orElse (Some why) }

    // At position i: how many characters to take, the next state, and how the taken characters appear in the code
    // (comments blanked) and the mask (literal contents blanked too).
    let decide i (s: ScanState) : int * ScanState * (char -> char) * (char -> char) =
        let c = text.[i]
        let next = at (i + 1)
        let mode, outer = List.head s.Stack, List.tail s.Stack
        let push m = { s with Stack = m :: s.Stack }
        let pop = { s with Stack = outer }
        let replace m = { s with Stack = m :: outer }

        // Code, or the code inside a template's `${...}`.
        let code () =
            match lang with
            | _ when c = '/' && next = '/' -> 2, push LineComment, blank, blank
            | Js when c = '/' && next = '*' -> 2, push (BlockComment 1), blank, blank
            | FSharp when c = '(' && next = '*' && at (i + 2) <> ')' -> 2, push (BlockComment 1), blank, blank
            | Js when c = '"' || c = '\'' -> 1, { push (Quoted c) with Prev = c }, id, id
            | Js when c = '`' -> 1, { push Template with Prev = c }, id, id
            | Js when c = '/' && s.Prev = afterAmbiguous ->
                1, fail { s with Prev = c } (sprintf "a `/` at offset %d follows a `}` that may close a block or an expression: division or a regular expression cannot be told" i), id, id
            | Js when c = '/' && (regexMayFollow s.Prev || (Char.IsLetter s.Prev && jsRegexKeywords.Contains(wordBefore i))) ->
                1, { push (RegexLit false) with Prev = c }, id, id
            | Js when c = '(' -> 1, { s with Parens = jsHeadKeywords.Contains(wordBefore i) :: s.Parens; Prev = c }, id, id
            | Js when c = ')' ->
                match s.Parens with
                | [] -> 1, fail { s with Prev = c } (sprintf "an unmatched `)` at offset %d" i), id, id
                | head :: rest -> 1, { s with Parens = rest; Prev = (if head then afterHead else ')') }, id, id
            | Js when c = '{' ->
                let kind =
                    match s.Prev with
                    | '\000' | ';' | '{' | '}' -> BlockBrace
                    | p when p = afterHead || p = afterAmbiguous -> BlockBrace
                    | '>' when arrowBefore i -> BlockBrace
                    | p when "(,=[?!&|+-*%<>~^/".IndexOf p >= 0 -> ExprBrace
                    | p when Char.IsLetter p || p = '_' || p = '$' ->
                        let word = wordBefore i
                        if jsBlockKeywords.Contains word then BlockBrace
                        elif jsRegexKeywords.Contains word then ExprBrace
                        else AmbiguousBrace
                    | _ -> AmbiguousBrace

                1, { s with Braces = kind :: s.Braces; Prev = c }, id, id
            | Js when c = '}' ->
                match s.Braces with
                | [] -> 1, fail { s with Prev = c } (sprintf "an unmatched `}` at offset %d" i), id, id
                | kind :: rest ->
                    let prev =
                        match kind with
                        | ExprBrace -> 'a'
                        | BlockBrace -> '}'
                        | AmbiguousBrace -> afterAmbiguous

                    1, { s with Braces = rest; Prev = prev }, id, id
            | FSharp when c = '"' && next = '"' && at (i + 2) = '"' -> 3, { push Triple with Prev = c }, id, id
            | FSharp when c = '@' && next = '"' -> 2, { push Verbatim with Prev = c }, id, id
            | FSharp when c = '"' -> 1, { push (Quoted '"') with Prev = c }, id, id
            | FSharp when c = '`' && next = '`' -> 2, { push Backticks with Prev = c }, id, id
            | FSharp when c = '\'' && next <> '\n' && (next = '\\' || at (i + 2) = '\'') -> 1, { push (Quoted '\'') with Prev = c }, id, id
            | _ -> 1, { s with Prev = (if Char.IsWhiteSpace c then s.Prev else c) }, id, id

        match mode with
        | Code -> code ()
        | TemplateExpr depth when c = '{' -> 1, { replace (TemplateExpr(depth + 1)) with Prev = c }, id, id
        | TemplateExpr depth when c = '}' -> 1, { (if depth = 1 then pop else replace (TemplateExpr(depth - 1))) with Prev = 'a' }, id, id
        | TemplateExpr _ -> code ()
        | LineComment -> if c = '\n' then 1, pop, id, id else 1, s, blank, blank
        | BlockComment depth ->
            match lang with
            | Js when c = '*' && next = '/' -> 2, pop, blank, blank
            | FSharp when c = '*' && next = ')' -> 2, (if depth = 1 then pop else replace (BlockComment(depth - 1))), blank, blank
            | FSharp when c = '(' && next = '*' -> 2, replace (BlockComment(depth + 1)), blank, blank
            | _ -> 1, s, blank, blank
        | Quoted q ->
            if c = '\\' then 2, s, id, lit
            elif c = q then 1, { pop with Prev = 'a' }, id, id
            elif c = '\n' && lang = Js then 1, fail { pop with Prev = 'a' } (sprintf "a string runs past its line at offset %d" i), id, id
            else 1, s, id, lit
        | Verbatim ->
            if c = '"' && next = '"' then 2, s, id, lit
            elif c = '"' then 1, { pop with Prev = 'a' }, id, id
            else 1, s, id, lit
        | Triple -> if c = '"' && next = '"' && at (i + 2) = '"' then 3, { pop with Prev = 'a' }, id, id else 1, s, id, lit
        | Template ->
            if c = '\\' then 2, s, id, lit
            elif c = '`' then 1, { pop with Prev = 'a' }, id, id
            elif c = '$' && next = '{' then 2, { push (TemplateExpr 1) with Prev = '(' }, id, lit
            else 1, s, id, lit
        | RegexLit inClass ->
            if c = '\\' then 2, s, id, lit
            elif c = '\n' then 1, fail { pop with Prev = 'a' } (sprintf "a regular expression runs past its line at offset %d" i), id, id
            elif c = '[' then 1, replace (RegexLit true), id, lit
            elif c = ']' then 1, replace (RegexLit false), id, lit
            elif c = '/' && not inClass then 1, { pop with Prev = 'a' }, id, id
            else 1, s, id, lit
        | Backticks -> if c = '`' && next = '`' then 2, { pop with Prev = 'a' }, id, id else 1, s, id, lit

    let rec go i (s: ScanState) (code: char list) (mask: char list) =
        if i >= n then
            // Input may end inside a line comment (no final newline): that closes it.
            let stack = match s.Stack with LineComment :: rest -> rest | stack -> stack

            let problem =
                s.Problem
                |> Option.orElse (match stack with [ Code ] -> None | m :: _ -> Some(sprintf "the text ends inside %s" (describeMode m)) | [] -> Some "the scanner lost its context")
                |> Option.orElse (if s.Parens.IsEmpty then None else Some(sprintf "%d `(` never closed" s.Parens.Length))
                |> Option.orElse (if s.Braces.IsEmpty then None else Some(sprintf "%d `{` never closed" s.Braces.Length))

            String(List.rev code |> Array.ofList), String(List.rev mask |> Array.ofList), problem
        else
            let k, s', toCode, toMask = decide i s
            // A character inside a literal both before and after it is the literal's content; its delimiters stay.
            let isComment = function LineComment | BlockComment _ -> true | _ -> false
            let comment = isComment (List.head s.Stack) || isComment (List.head s'.Stack)
            let literal = List.exists isLiteral s.Stack && List.exists isLiteral s'.Stack
            let maskOf ch = if comment then blank ch elif literal then lit ch else toMask ch
            let taken = [ for j in i .. min (n - 1) (i + k - 1) -> text.[j] ]
            go (i + k) s' (List.fold (fun acc ch -> toCode ch :: acc) code taken) (List.fold (fun acc ch -> maskOf ch :: acc) mask taken)

    go 0 { Stack = [ Code ]; Parens = []; Braces = []; Prev = '\000'; Problem = None } [] []

let private collapse (s: string) = Regex.Replace(s, @"\s+", " ").Trim()

let private delta (opening: char list) (closing: char list) (s: string) =
    s |> Seq.sumBy (fun c -> if List.contains c opening then 1 elif List.contains c closing then -1 else 0)

/// Splits the lines of one block, (code, mask) pairs in order, into whole statements (code, mask).
let private statementsOf (lang: Language) (lines: (string * string) list) : (string * string) list =
    let indent (s: string) = s.Length - s.TrimStart().Length

    let continues (first: string) (joinedMask: string) (depth: int) (next: string option) =
        let m = joinedMask.TrimEnd()
        let nextTrim = next |> Option.map (fun s -> s.Trim()) |> Option.defaultValue ""
        let nextStarts (prefixes: string list) = next.IsSome && prefixes |> List.exists (fun p -> nextTrim.StartsWith(p, StringComparison.Ordinal))
        let endsWith (suffixes: string list) = suffixes |> List.exists (fun s -> m.EndsWith(s, StringComparison.Ordinal))

        match lang with
        | Js ->
            if Regex.IsMatch(m, @"(=>|\)|\belse|\btry|\bfinally|\bdo)\s*\{$") then false
            elif depth > 0 then true
            elif m.EndsWith(";", StringComparison.Ordinal) then false
            elif endsWith [ "&&"; "||"; "??"; ","; "+"; "-"; "*"; "="; "?"; ":"; "("; "["; "."; "!" ] then true
            elif Regex.IsMatch(m.TrimStart(), @"^(if|for|while|else|do)\b") && not (endsWith [ "{"; "}"; ";" ]) then true
            else nextStarts [ "."; "?."; "&&"; "||"; "??"; "?"; ":"; "+"; ")"; "]" ]
        | FSharp ->
            if depth > 0 then true
            elif nextStarts [ "|>"; "||"; "&&"; "."; ")"; "]"; "<|"; ">>"; "+"; "@>" ] then true
            elif endsWith [ "="; "->"; " do"; " then"; " else"; " with"; " try"; " finally" ] then false
            else next.IsSome && indent (Option.get next) > indent first

    let rec go (rest: (string * string) list) (current: (string * string) list) (depth: int) (out: (string * string) list) =
        let joinedPair (lines: (string * string) list) =
            collapse (lines |> List.map fst |> String.concat " "), collapse (lines |> List.map snd |> String.concat " ")

        let flush () =
            match current with
            | [] -> out
            | lines -> joinedPair (List.rev lines) :: out

        match rest with
        | [] -> List.rev (flush ())
        | (_, m) :: tail when String.IsNullOrWhiteSpace m -> go tail current depth out
        | (c, m) :: tail ->
            let current' = (c, m) :: current
            let depth' = depth + delta [ '('; '[' ] [ ')'; ']' ] m
            let ordered = List.rev current'
            let first = ordered |> List.head |> snd
            let joined = ordered |> List.map snd |> String.concat " "
            let next = tail |> List.map snd |> List.tryFind (String.IsNullOrWhiteSpace >> not)

            if continues first joined depth' next then
                go tail current' depth' out
            else
                let out' = joinedPair ordered :: out
                go tail [] 0 out'

    go lines [] 0 []

/// How a block is marked: not at all, by a conditional skip call only (`test.skip(isMobile, "...")`: whether it
/// runs depends on its argument), or unconditionally. Ordered, so the stronger of two marks is their `max`.
type Mark =
    | Unmarked
    | Conditional
    | Unconditional

/// One block while reading a file: its owner index is its position in the list.
type private Draft =
    { Title: string
      Kind: Kind
      Marked: Mark
      Parent: int }

let private jsStart = Regex(@"^\s*((?:test|it|describe|xit|xtest|xdescribe|fit)(?:\.[A-Za-z]+)*)\s*\(\s*(?:[""'`]|$)", RegexOptions.Compiled)
let private jsLeadingTitle = Regex(@"^\s*([""'`])((?:\\.|(?!\1).)*)\1", RegexOptions.Compiled)
let private jsTitle = Regex(@"^\s*[\w.]+\s*\(\s*([""'`])((?:\\.|(?!\1).)*)\1", RegexOptions.Compiled)
let private jsMarkers = set [ "skip"; "fixme"; "fail"; "only" ]
let private jsNotBlocks = set [ "step"; "beforeEach"; "afterEach"; "beforeAll"; "afterAll"; "use"; "extend"; "info" ]

/// A call that skips (or expects to fail) the test or block it runs in, anywhere in a masked line: any member call
/// `.skip(`, `.fixme(`, `.fail(` (`test.skip()`, `testInfo.skip()` whatever the fixture parameter is named,
/// `test.info().skip()`, `this.skip()`), and `test.only(`/`describe.only(`.
let private jsBodyMarker =
    Regex(@"\.\s*(?:skip|fixme|fail)\s*\(|(?<![\w$.])(?:test|it|describe)(?:\.describe)?\.only\s*\(", RegexOptions.Compiled)

let private jsDestructured = Regex(@"\b(?:const|let|var)\s*\{([^{}]*)\}\s*=", RegexOptions.Compiled)
let private jsBoundMarker = Regex(@"\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*[\w$.()]*\.(?:skip|fixme|fail)\b(?!\s*\()", RegexOptions.Compiled)

/// The local names a file binds to a skip marker: `const { skip } = test`, `const { fixme: later } = test`,
/// `const skip = test.skip`. A call of one marks its scope as a call of the marker would.
let private jsMarkerAliases (mask: string) : string list =
    let destructured =
        [ for m in jsDestructured.Matches mask do
              for entry in m.Groups.[1].Value.Split(',') do
                  match entry.Split(':') |> Array.map _.Trim() with
                  | [| key |] when jsMarkers.Contains key && key <> "only" -> yield key
                  | [| key; local |] when jsMarkers.Contains key && key <> "only" && Regex.IsMatch(local, @"^[A-Za-z_$][\w$]*$") -> yield local
                  | _ -> () ]

    destructured @ [ for m in jsBoundMarker.Matches mask -> m.Groups.[1].Value ] |> List.distinct

/// (blocks, owner of each line (-1 = module), whether the whole file is marked)
let private readJs (code: string[]) (mask: string[]) =
    let pairs = Array.zip code mask

    let markers =
        match jsMarkerAliases (String.concat "\n" mask) with
        | [] -> [ jsBodyMarker ]
        | aliases -> [ jsBodyMarker; Regex(sprintf @"(?<![\w$.])(?:%s)\s*\(" (aliases |> List.map Regex.Escape |> String.concat "|")) ]

    // The strongest mark a masked line carries: a marker call with no argument (or `.only`) is unconditional.
    let marks (m: string) : Mark =
        markers
        |> List.collect (fun rx -> [ for hit in rx.Matches m -> hit ])
        |> List.fold
            (fun acc hit ->
                let after = m.Substring(hit.Index + hit.Length).TrimStart()
                max acc (if hit.Value.Contains "only" || after.StartsWith ")" then Unconditional else Conditional))
            Unmarked

    // Each line with the next non-blank line: a declaration may put its title on the line after `test(`.
    let following =
        Array.foldBack (fun (c, m: string) (acc, after) -> after :: acc, (if String.IsNullOrWhiteSpace m then after else Some(c, m))) pairs ([], None) |> fst

    let lines = List.zip (List.ofArray pairs) following

    let step (depth: int, stack: (int * int) list, drafts: Draft list, owned: (int * string * string) list, fileMarked: Mark) ((c: string, m: string), next: (string * string) option) =
        let owner = match stack with (b, _) :: _ -> b | [] -> -1
        let start = jsStart.Match m
        let head = if start.Success then start.Groups.[1].Value.Split('.') |> List.ofArray else []
        let titleBelow = start.Success && m.TrimEnd().EndsWith "("
        let nextTitle = next |> Option.map (fst >> jsLeadingTitle.Match) |> Option.filter _.Success
        let isBlock = start.Success && not (head |> List.exists jsNotBlocks.Contains) && (not titleBelow || nextTitle.IsSome)
        let only = Regex.IsMatch(m, @"\b(?:test|it|describe)(?:\.describe)?\.only\b")
        let depth' = depth + delta [ '(' ] [ ')' ] m
        let pop (s: (int * int) list) = s |> List.skipWhile (fun (_, d) -> d >= depth')

        if isBlock then
            let id = drafts.Length
            let title =
                match titleBelow, nextTitle with
                | true, Some t -> t.Groups.[2].Value
                | _ -> let t = jsTitle.Match c in if t.Success then t.Groups.[2].Value else ""
            let kind = if head |> List.exists (fun p -> p = "describe" || p = "xdescribe") then Describe else Test
            let marked = if head |> List.exists jsMarkers.Contains || head.Head.StartsWith "x" then Unconditional else Unmarked
            // The body may begin on the declaration line, after the function's opening brace.
            let arrow = m.LastIndexOf "=>"
            let brace = if arrow >= 0 then m.IndexOf('{', arrow) else m.LastIndexOf '{'
            let rest = if brace >= 0 then c.Substring(brace + 1), m.Substring(brace + 1) else "", ""

            depth',
            pop ((id, depth) :: stack),
            drafts @ [ { Title = title; Kind = kind; Marked = max marked (marks (snd rest)); Parent = owner } ],
            (id, fst rest, snd rest) :: owned,
            (if only then Unconditional else fileMarked)
        else
            let mark = marks m
            let drafts' =
                if mark <> Unmarked && owner >= 0 then drafts |> List.mapi (fun i d -> if i = owner then { d with Marked = max d.Marked mark } else d)
                else drafts

            depth', pop stack, drafts', (owner, c, m) :: owned, max fileMarked (if only then Unconditional elif owner < 0 then mark else Unmarked)

    let _, _, drafts, owned, fileMarked = lines |> List.fold step (0, [], [], [], Unmarked)
    drafts, List.rev owned, fileMarked

let private fsStart = Regex(@"^(\s*)(?:let|member\s+[\w.]+)\s+``(.+?)``", RegexOptions.Compiled)
let private fsSkip = Regex(@"\bSkip\s*=", RegexOptions.Compiled)

let private readFSharp (code: string[]) (mask: string[]) =
    let indent (s: string) = s.Length - s.TrimStart().Length

    let step (current: (int * int) option, pendingSkip: bool, drafts: Draft list, owned: (int * string * string) list) (c: string, m: string) =
        let start = fsStart.Match c

        if String.IsNullOrWhiteSpace m then
            current, pendingSkip, drafts, ((current |> Option.map fst |> Option.defaultValue -1), c, m) :: owned
        elif start.Success then
            let id = drafts.Length
            let marked = if pendingSkip || fsSkip.IsMatch c then Unconditional else Unmarked
            Some(id, indent c), false, drafts @ [ { Title = start.Groups.[2].Value; Kind = Test; Marked = marked; Parent = -1 } ], (id, "", "") :: owned
        else
            match current with
            | Some(id, ind) when indent c > ind -> current, pendingSkip, drafts, (id, c, m) :: owned
            | _ ->
                let attribute = c.TrimStart().StartsWith("[<", StringComparison.Ordinal)
                None, (attribute && (pendingSkip || fsSkip.IsMatch c)), drafts, (-1, c, m) :: owned

    let _, _, drafts, owned = Array.zip code mask |> Array.fold step (None, false, [], [])
    drafts, List.rev owned, Unmarked

/// The module name a file's out-of-test lines are tracked under: the F# `module`, else the file name.
let private moduleName (lang: Language) (path: string) (code: string[]) =
    let declared =
        match lang with
        | FSharp -> code |> Array.tryPick (fun l -> let m = Regex.Match(l, @"^module\s+(?:rec\s+)?([\w.]+)") in if m.Success then Some m.Groups.[1].Value else None)
        | Js -> None

    sprintf "%s::%s" (Path.GetFileName path) (declared |> Option.defaultValue "module")

/// What a file holds: its blocks; or that it could not be read consistently (UNPARSEABLE, see scan); or that it is
/// in a language this module does not read (JSX/TSX). Neither of the last two reports any block, so no caller can
/// mistake a desynchronised or unsupported reading for the file's tests.
type Reading =
    | Blocks of Block list
    | Unparseable of problem: string
    | Unsupported of reason: string

/// The blocks of one file: each test and describe block, then the file's module block. None for a file that is not
/// source code at all.
let parse (path: string) (text: string) : Reading option =
    if isUnsupported path then
        Some(Unsupported "JSX/TSX is not read: its markup quoting is not lexed")
    else
        languageOf path
        |> Option.map (fun lang ->
            match scan lang (text.Replace("\r\n", "\n")) with
            | _, _, Some problem -> Unparseable problem
            | code, mask, None ->
                let codeLines, maskLines = code.Split('\n'), mask.Split('\n')
                let drafts, owned, fileMarked = (match lang with Js -> readJs | FSharp -> readFSharp) codeLines maskLines
                let byIndex = Array.ofList drafts
                let rec markOf i = if i < 0 then Unmarked else max byIndex.[i].Marked (markOf byIndex.[i].Parent)
                let statements owner = owned |> List.filter (fun (o, _, _) -> o = owner) |> List.map (fun (_, c, m) -> c, m) |> statementsOf lang

                let block title kind (mark: Mark) owner =
                    let pairs = statements owner

                    { Title = title
                      Kind = kind
                      Skipped = mark <> Unmarked
                      SkipConditional = mark = Conditional
                      Statements = List.map fst pairs
                      Masked = List.map snd pairs }

                Blocks(
                    (drafts |> List.mapi (fun i d -> block d.Title d.Kind (max fileMarked (markOf i)) i))
                    @ [ block (moduleName lang path codeLines) Module Unmarked -1 ]
                ))

let private jsAssertion = Regex(@"(?<![\w$.])(?:expect|assert)\b\s*(?:\.\s*\w+\s*)?\(", RegexOptions.Compiled)
let private fsAssertion = Regex(@"\bAssert\.|\bExpect\.|\btest\s*<@|\|>\s*should\b|\bshould\s*\(", RegexOptions.Compiled)

/// The statements of a block that assert. The assertion is looked for in the masked statement, so an assertion
/// quoted in a string or template literal asserts nothing.
let assertions (path: string) (block: Block) : string list =
    let rx = match languageOf path with Some FSharp -> fsAssertion | _ -> jsAssertion
    List.zip block.Statements block.Masked |> List.filter (snd >> rx.IsMatch) |> List.map fst
