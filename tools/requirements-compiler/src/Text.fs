/// Pure text helpers used for normalization and matching.
module Text

open System
open System.Text
open System.Text.RegularExpressions

let utf8 (bytes: byte[]) = Encoding.UTF8.GetString bytes

let normalizeNewlines (s: string) = s.Replace("\r\n", "\n").Replace("\r", "\n")

let sha256OfText (s: string) =
    normalizeNewlines s |> Encoding.UTF8.GetBytes |> Git.sha256Hex

let collapseWhitespace (s: string) = Regex.Replace(s, @"\s+", " ").Trim()

/// Lower-case, markdown-free, whitespace-collapsed form used to compare obligations.
let canonical (s: string) =
    let noMarkup = Regex.Replace(s, @"[*`_]", "")
    (collapseWhitespace noMarkup).TrimEnd('.', ';', ':', ' ').ToLowerInvariant()

let slug (s: string) =
    let lowered = s.ToLowerInvariant()
    Regex.Replace(Regex.Replace(lowered, @"[^a-z0-9]+", "-"), @"-+", "-").Trim('-')

let private stopwords =
    set [ "the"; "and"; "for"; "with"; "that"; "this"; "from"; "are"; "not"; "must"; "should"; "may"; "into"; "each"
          "every"; "any"; "all"; "its"; "their"; "than"; "then"; "when"; "only"; "has"; "have"; "was"; "were"; "will"
          "can"; "does"; "but"; "nor"; "use"; "via"; "per"; "never"; "without"; "before"; "after" ]

let contentWords (s: string) : Set<string> =
    Regex.Matches((canonical s), @"[a-z0-9][a-z0-9\-]{2,}")
    |> Seq.map (fun m -> m.Value)
    |> Seq.filter (fun w -> not (stopwords.Contains w))
    |> Set.ofSeq

let jaccard (a: Set<string>) (b: Set<string>) =
    if a.IsEmpty || b.IsEmpty then 0.0
    else float (Set.intersect a b).Count / float (Set.union a b).Count

/// Citation quotes are matched after whitespace and markdown emphasis are removed on both sides.
let containsQuote (haystack: string) (quote: string) =
    let squash (s: string) = collapseWhitespace (Regex.Replace(s, @"[*`]", ""))
    (squash haystack).Contains(squash quote, StringComparison.Ordinal)

type Modality =
    | Must
    | Should
    | May
    | Unmarked

let modalityName =
    function
    | Must -> "must"
    | Should -> "should"
    | May -> "may"
    | Unmarked -> "unmarked"

let modalityRank =
    function
    | Must -> 3
    | Should -> 2
    | May -> 1
    | Unmarked -> 0

/// Reads the modality the text states. Upper-case RFC 2119 words and imperative prohibitions
/// ("never", "do not") count as Must; nothing is promoted beyond what the text says.
let modalityOf (text: string) =
    if Regex.IsMatch(text, @"\b(MUST|SHALL|REQUIRED)\b") then Must
    elif Regex.IsMatch(text, @"\b(SHOULD|RECOMMENDED)\b") then Should
    elif Regex.IsMatch(text, @"\bMAY\b") then May
    elif Regex.IsMatch(text, @"(?i)\b(must|shall|never|do not|does not|cannot|is required|are required|no agent may)\b") then Must
    elif Regex.IsMatch(text, @"(?i)\b(should|prefer|recommended)\b") then Should
    elif Regex.IsMatch(text, @"(?i)\bmay\b") then May
    else Unmarked

let vagueTerms =
    [ "appropriate"; "where practical"; "where applicable"; "where supported"; "where feasible"; "if feasible"
      "as needed"; "as required"; "reasonable"; "quickly"; "fast"; "seconds"; "user-friendly"; "intuitive"
      "professional"; "polished"; "clear"; "clearly"; "minimal"; "significant"; "relevant"; "etc"; "and similar"
      "as policy requires"; "where necessary"; "if necessary"; "sufficient" ]

let vagueTermsIn (text: string) =
    let lowered = text.ToLowerInvariant()
    vagueTerms
    |> List.filter (fun t -> Regex.IsMatch(lowered, sprintf @"(?<![a-z\-])%s(?![a-z\-])" (Regex.Escape t)))

let private hasAny (patterns: string list) (text: string) =
    patterns |> List.exists (fun p -> Regex.IsMatch(text, p, RegexOptions.IgnoreCase))

let mentionsSecurity =
    hasAny [ @"\bcredential"; @"\btoken"; @"\bsecret"; @"\bauthori[sz]"; @"\bauthenticat"; @"least[- ]privilege"; @"\bforged"; @"\bunauthori[sz]ed"; @"\bpermission" ]

let mentionsAccessibility =
    hasAny [ @"accessib"; @"\bWCAG\b"; @"\bkeyboard"; @"screen reader"; @"\bcontrast"; @"reduced motion"; @"\bfocus\b"; @"\bARIA\b"; @"\ba11y\b" ]

let mentionsErrorHandling =
    hasAny [ @"\bfail"; @"\berror"; @"\brefus"; @"\breject"; @"\bconflict"; @"\boffline"; @"\brecover"; @"\bfault"; @"\bdenial"; @"\bmalformed"; @"\bblocked\b"; @"\bdegraded" ]

let mentionsTiming = hasAny [ @"\b\d+(\.\d+)?\s*(ms|s|seconds?|minutes?|min)\b" ]

/// Sentences of a line: split after terminal punctuation followed by space and an upper-case or markup start.
let sentences (line: string) =
    Regex.Split(line, @"(?<=[.!?])\s+(?=[A-Z*`\[(])")
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (fun s -> s <> "")
    |> List.ofArray
