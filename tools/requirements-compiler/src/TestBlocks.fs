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
    | ".cts"
    | ".jsx"
    | ".tsx" -> Some Js
    | ".fs"
    | ".fsx" -> Some FSharp
    | _ -> None

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

/// The text with comments blanked (`code`) and the same text with literal contents blanked as well (`mask`). Both keep
/// the length and every newline, so line i of one is line i of the other.
let scan (lang: Language) (text: string) : string * string =
    let n = text.Length
    let at i = if i < n then text.[i] else '\000'
    let regexMayFollow (prev: char) = prev = '\000' || "(,=:[!&|?{};+-*%<>~^".IndexOf prev >= 0
    let blank c = if c = '\n' then '\n' else ' '
    let lit c = if c = '\n' then '\n' else '_'

    let finish (code: char list) (mask: char list) =
        String(List.rev code |> Array.ofList), String(List.rev mask |> Array.ofList)

    // At position i in `mode`: how many characters to take, the next mode and previous significant character, and how
    // the taken characters appear in the code and in the mask.
    let decide i mode (prev: char) : int * Mode * char * (char -> char) * (char -> char) =
        let c = text.[i]
        let next = at (i + 1)

        match mode with
        | Code ->
            match lang with
            | _ when c = '/' && next = '/' -> 2, LineComment, prev, blank, blank
            | Js when c = '/' && next = '*' -> 2, BlockComment 1, prev, blank, blank
            | FSharp when c = '(' && next = '*' && at (i + 2) <> ')' -> 2, BlockComment 1, prev, blank, blank
            | Js when c = '"' || c = '\'' -> 1, Quoted c, c, id, id
            | Js when c = '`' -> 1, Template, c, id, id
            | Js when c = '/' && regexMayFollow prev -> 1, RegexLit false, c, id, id
            | FSharp when c = '"' && next = '"' && at (i + 2) = '"' -> 3, Triple, c, id, id
            | FSharp when c = '@' && next = '"' -> 2, Verbatim, c, id, id
            | FSharp when c = '"' -> 1, Quoted '"', c, id, id
            | FSharp when c = '`' && next = '`' -> 2, Backticks, c, id, id
            | FSharp when c = '\'' && next <> '\n' && (next = '\\' || at (i + 2) = '\'') -> 1, Quoted '\'', c, id, id
            | _ -> 1, Code, (if Char.IsWhiteSpace c then prev else c), id, id
        | LineComment -> if c = '\n' then 1, Code, prev, id, id else 1, LineComment, prev, blank, blank
        | BlockComment depth ->
            match lang with
            | Js when c = '*' && next = '/' -> 2, Code, prev, blank, blank
            | FSharp when c = '*' && next = ')' -> 2, (if depth = 1 then Code else BlockComment(depth - 1)), prev, blank, blank
            | FSharp when c = '(' && next = '*' -> 2, BlockComment(depth + 1), prev, blank, blank
            | _ -> 1, mode, prev, blank, blank
        | Quoted q ->
            if c = '\\' then 2, mode, prev, id, lit
            elif c = q then 1, Code, 'a', id, id
            elif c = '\n' && lang = Js then 1, Code, 'a', id, id
            else 1, mode, prev, id, lit
        | Verbatim ->
            if c = '"' && next = '"' then 2, Verbatim, prev, id, lit
            elif c = '"' then 1, Code, 'a', id, id
            else 1, Verbatim, prev, id, lit
        | Triple -> if c = '"' && next = '"' && at (i + 2) = '"' then 3, Code, 'a', id, id else 1, Triple, prev, id, lit
        | Template ->
            if c = '\\' then 2, Template, prev, id, lit
            elif c = '`' then 1, Code, 'a', id, id
            elif c = '$' && next = '{' then 2, TemplateExpr 1, prev, id, lit
            else 1, Template, prev, id, lit
        | TemplateExpr depth ->
            if c = '{' then 1, TemplateExpr(depth + 1), prev, id, lit
            elif c = '}' then 1, (if depth = 1 then Template else TemplateExpr(depth - 1)), prev, id, lit
            else 1, mode, prev, id, lit
        | RegexLit inClass ->
            if c = '\\' then 2, mode, prev, id, lit
            elif c = '\n' then 1, Code, 'a', id, id
            elif c = '[' then 1, RegexLit true, prev, id, lit
            elif c = ']' then 1, RegexLit false, prev, id, lit
            elif c = '/' && not inClass then 1, Code, 'a', id, id
            else 1, mode, prev, id, lit
        | Backticks -> if c = '`' && next = '`' then 2, Code, 'a', id, id else 1, Backticks, prev, id, lit

    let rec go i mode (prev: char) (code: char list) (mask: char list) =
        if i >= n then
            finish code mask
        else
            let k, mode', prev', toCode, toMask = decide i mode prev
            let taken = [ for j in i .. min (n - 1) (i + k - 1) -> text.[j] ]
            go (i + k) mode' prev' (List.fold (fun acc ch -> toCode ch :: acc) code taken) (List.fold (fun acc ch -> toMask ch :: acc) mask taken)

    go 0 Code '\000' [] []

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

/// One block while reading a file: its owner index is its position in the list.
type private Draft =
    { Title: string
      Kind: Kind
      Marked: bool
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

    let marks =
        match jsMarkerAliases (String.concat "\n" mask) with
        | [] -> fun (m: string) -> jsBodyMarker.IsMatch(m)
        | aliases ->
            let aliased = Regex(sprintf @"(?<![\w$.])(?:%s)\s*\(" (aliases |> List.map Regex.Escape |> String.concat "|"))
            fun (m: string) -> jsBodyMarker.IsMatch m || aliased.IsMatch(m)

    // Each line with the next non-blank line: a declaration may put its title on the line after `test(`.
    let following =
        Array.foldBack (fun (c, m: string) (acc, after) -> after :: acc, (if String.IsNullOrWhiteSpace m then after else Some(c, m))) pairs ([], None) |> fst

    let lines = List.zip (List.ofArray pairs) following

    let step (depth: int, stack: (int * int) list, drafts: Draft list, owned: (int * string * string) list, fileMarked: bool) ((c: string, m: string), next: (string * string) option) =
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
            let marked = head |> List.exists jsMarkers.Contains || head.Head.StartsWith "x"
            // The body may begin on the declaration line, after the function's opening brace.
            let arrow = m.LastIndexOf "=>"
            let brace = if arrow >= 0 then m.IndexOf('{', arrow) else m.LastIndexOf '{'
            let rest = if brace >= 0 then c.Substring(brace + 1), m.Substring(brace + 1) else "", ""

            depth',
            pop ((id, depth) :: stack),
            drafts @ [ { Title = title; Kind = kind; Marked = marked || marks (snd rest); Parent = owner } ],
            (id, fst rest, snd rest) :: owned,
            fileMarked || only
        else
            let drafts' =
                if marks m && owner >= 0 then drafts |> List.mapi (fun i d -> if i = owner then { d with Marked = true } else d)
                else drafts

            depth', pop stack, drafts', (owner, c, m) :: owned, fileMarked || only || (marks m && owner < 0)

    let _, _, drafts, owned, fileMarked = lines |> List.fold step (0, [], [], [], false)
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
            Some(id, indent c), false, drafts @ [ { Title = start.Groups.[2].Value; Kind = Test; Marked = pendingSkip || fsSkip.IsMatch c; Parent = -1 } ], (id, "", "") :: owned
        else
            match current with
            | Some(id, ind) when indent c > ind -> current, pendingSkip, drafts, (id, c, m) :: owned
            | _ ->
                let attribute = c.TrimStart().StartsWith("[<", StringComparison.Ordinal)
                None, (attribute && (pendingSkip || fsSkip.IsMatch c)), drafts, (-1, c, m) :: owned

    let _, _, drafts, owned = Array.zip code mask |> Array.fold step (None, false, [], [])
    drafts, List.rev owned, false

/// The module name a file's out-of-test lines are tracked under: the F# `module`, else the file name.
let private moduleName (lang: Language) (path: string) (code: string[]) =
    let declared =
        match lang with
        | FSharp -> code |> Array.tryPick (fun l -> let m = Regex.Match(l, @"^module\s+(?:rec\s+)?([\w.]+)") in if m.Success then Some m.Groups.[1].Value else None)
        | Js -> None

    sprintf "%s::%s" (Path.GetFileName path) (declared |> Option.defaultValue "module")

/// The blocks of one file: each test and describe block, then the file's module block. None for a file in no
/// language this module reads.
let parse (path: string) (text: string) : Block list option =
    languageOf path
    |> Option.map (fun lang ->
        let code, mask = scan lang (text.Replace("\r\n", "\n"))
        let codeLines, maskLines = code.Split('\n'), mask.Split('\n')
        let drafts, owned, fileMarked = (match lang with Js -> readJs | FSharp -> readFSharp) codeLines maskLines
        let byIndex = Array.ofList drafts
        let rec skipped i = i >= 0 && (byIndex.[i].Marked || skipped byIndex.[i].Parent)
        let statements owner = owned |> List.filter (fun (o, _, _) -> o = owner) |> List.map (fun (_, c, m) -> c, m) |> statementsOf lang
        let block title kind skipped owner =
            let pairs = statements owner
            { Title = title; Kind = kind; Skipped = skipped; Statements = List.map fst pairs; Masked = List.map snd pairs }

        (drafts |> List.mapi (fun i d -> block d.Title d.Kind (fileMarked || skipped i) i))
        @ [ block (moduleName lang path codeLines) Module false -1 ])

let private jsAssertion = Regex(@"(?<![\w$.])(?:expect|assert)\b\s*(?:\.\s*\w+\s*)?\(", RegexOptions.Compiled)
let private fsAssertion = Regex(@"\bAssert\.|\bExpect\.|\btest\s*<@|\|>\s*should\b|\bshould\s*\(", RegexOptions.Compiled)

/// The statements of a block that assert. The assertion is looked for in the masked statement, so an assertion
/// quoted in a string or template literal asserts nothing.
let assertions (path: string) (block: Block) : string list =
    let rx = match languageOf path with Some FSharp -> fsAssertion | _ -> jsAssertion
    List.zip block.Statements block.Masked |> List.filter (snd >> rx.IsMatch) |> List.map fst
