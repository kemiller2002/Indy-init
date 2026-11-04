/// The governed design contract (adversarial audit R5; SA-05).
///
/// Rehearsal 009 shipped a page with no Forma navigation shell, no Forma save-status component, no Administration or
/// Settings and no UI-016 palette, and nothing inspected markup or CSS. This is the static half of the check; the
/// rendered half (layout, overflow, touch targets, focus, contrast, screenshots) is verification/design-contract,
/// run in a browser from the governed checkout. The two verdicts combine in `design-contract`.
///
/// The contract names roles, not one element spelling: each role lists the names the approved Forma versions use, so
/// a newer compatible Forma that renames an element passes once its name is added under owner review. Colours are not
/// banned outright: literal colours are confined to the theme boundary (custom-property definitions in the declared
/// theme files), and every `var(--…)` must resolve to a theme token or a pinned Forma property.
module DesignContract

open System
open System.Text.RegularExpressions
open Json
open Scope

type Role = { Role: string; Names: string list; Requirement: string }

type Contract =
    { FormaPackage: string
      /// Accepted Forma versions as prefixes ("0.12.", "0.13.").
      AcceptedVersions: string list
      Roles: Role list
      Destinations: string list
      DestinationsRequirement: string
      /// The investigation sections (UI-029 secondary navigation).
      Sections: string list
      /// (role, hex) for the UI-016 canonical palette.
      Palette: (string * string) list
      /// Substrings that identify the theme files (the only place a literal colour may be defined).
      ThemeFiles: string list
      /// The pinned Forma custom properties (the inventory) a `var(--ef-…)` may use.
      FormaProperties: Set<string> }

let contractOf (j: Json) (formaInventory: Json) : Contract =
    let forma = field "forma" j |> Option.defaultValue (JObj [])
    { FormaPackage = strOpt "package" forma |> Option.defaultValue "@echelon-foundry/design-system"
      AcceptedVersions = strs "acceptedVersions" forma
      Roles = arr "roles" j |> List.map (fun r -> { Role = str "role" r; Names = strs "names" r; Requirement = strOpt "requirement" r |> Option.defaultValue "" })
      Destinations = field "primaryDestinations" j |> Option.map (strs "names") |> Option.defaultValue []
      DestinationsRequirement = field "primaryDestinations" j |> Option.bind (strOpt "requirement") |> Option.defaultValue ""
      Sections = field "investigationSections" j |> Option.map (strs "names") |> Option.defaultValue []
      Palette = arr "palette" j |> List.map (fun p -> str "role" p, (str "hex" p).ToUpperInvariant())
      ThemeFiles = strs "themeFiles" j
      FormaProperties = (strs "customProperties" formaInventory @ strs "hookProperties" formaInventory) |> Set.ofList }

type SourceFile = { Path: string; Text: string }

let private refusal code detail = { Code = code; Detail = detail }

let private literalColour = Regex(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\s*\(", RegexOptions.Compiled)
let private declaration = Regex(@"(?<prop>-{0,2}[A-Za-z][A-Za-z0-9_-]*)\s*:\s*(?<value>[^;{}]+)", RegexOptions.Compiled)
let private varUse = Regex(@"var\(\s*(?<name>--[A-Za-z0-9_-]+)", RegexOptions.Compiled)
let private stripComments (css: string) = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline)
/// A literal colour that is only the fallback of a var() (var(--x, #fff)) is not a use of a literal.
let private withoutFallbacks (value: string) = Regex.Replace(value, @"var\([^()]*\)", "var()")

/// Markup and code with comments removed (HTML, CSS/JS block, JS/F# line and F# block comments), so a name that
/// appears only in a comment does not count as used.
let private withoutComments (text: string) =
    let t = Regex.Replace(text, @"<!--.*?-->|/\*.*?\*/|\(\*.*?\*\)", "", RegexOptions.Singleline)
    Regex.Replace(t, @"(?<![:""'\\])//[^\n]*", "")

/// A Forma name is used when it appears as a class or element name, or inside a string literal of the view code.
let private usesName (code: string) (name: string) =
    let n = Regex.Escape name
    Regex.IsMatch(code, @"<" + n + @"[\s>/]|[""'`][^""'`\n]*(?<![\w-])" + n + @"(?![\w-])[^""'`\n]*[""'`]")

let isTheme (c: Contract) (path: string) = c.ThemeFiles |> List.exists (fun t -> path.Contains(t, StringComparison.OrdinalIgnoreCase))

let private lineOf (text: string) (index: int) = text.Substring(0, index).Split('\n').Length

/// The static checks. `files` are the candidate's served sources (HTML, CSS, JS and the F# view sources);
/// `packageJson` is the candidate's root package.json.
let check (c: Contract) (files: SourceFile list) (packageJson: Json option) : Refusal list =
    let css = files |> List.filter (fun f -> f.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
    let markup = files |> List.filter (fun f -> not (f.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
    let allMarkup = markup |> List.map (fun f -> withoutComments f.Text) |> String.concat "\n"
    let html = files |> List.filter (fun f -> f.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
    let version =
        packageJson
        |> Option.bind (fun p -> [ "dependencies"; "devDependencies" ] |> List.tryPick (fun k -> field k p |> Option.bind (strOpt c.FormaPackage)))
    let themeDefs =
        css
        |> List.filter (fun f -> isTheme c f.Path)
        |> List.collect (fun f -> [ for m in declaration.Matches(stripComments f.Text) do if m.Groups["prop"].Value.StartsWith "--" then yield m.Groups["prop"].Value, m.Groups["value"].Value.Trim() ])
    // Any custom property the candidate defines is a token (spacing, sizes); colour literals are confined separately.
    let themeTokens =
        css |> List.collect (fun f -> [ for m in declaration.Matches(stripComments f.Text) do if m.Groups["prop"].Value.StartsWith "--" then yield m.Groups["prop"].Value ]) |> Set.ofList
    [ // The approved Forma package, at an accepted version, loaded by the page.
      match version with
      | None -> yield refusal "FORMA_NOT_DECLARED" (sprintf "package.json does not depend on %s" c.FormaPackage)
      | Some v when [ "||"; ">"; "<"; "*"; " - "; "x"; "X" ] |> List.exists v.Contains || not (c.AcceptedVersions |> List.exists (fun p -> v.TrimStart('^', '~', '=').StartsWith p)) ->
          yield refusal "FORMA_VERSION_UNAPPROVED" (sprintf "%s %s is not an approved version (%s)" c.FormaPackage v (String.concat ", " c.AcceptedVersions))
      | Some _ -> ()
      if not (html |> List.exists (fun f -> Regex.IsMatch(withoutComments f.Text, @"<link[^>]+href\s*=\s*[""'][^""']*(@echelon-foundry/design-system|design-system/|/forma/)[^""']*\.css", RegexOptions.IgnoreCase))) then
          yield refusal "FORMA_NOT_LOADED" "no page loads the Forma stylesheet"
      // Forma roles: the application shell and the save-status presentation.
      for r in c.Roles do
          if not (r.Names |> List.exists (usesName allMarkup)) then
              yield refusal "FORMA_ROLE_MISSING" (sprintf "%s (%s): no source uses %s; a lookalike custom component does not satisfy it" r.Role r.Requirement (String.concat " or " r.Names))
      // The required primary destinations.
      for d in c.Destinations do
          if not (Regex.IsMatch(allMarkup, @">\s*" + Regex.Escape d + @"\s*<|[""'`]" + Regex.Escape d + @"[""'`]")) then
              yield refusal "DESTINATION_MISSING" (sprintf "primary destination %s (%s) appears in no source" d c.DestinationsRequirement)
      for s in c.Sections do
          if not (Regex.IsMatch(allMarkup, @">\s*" + Regex.Escape s + @"\s*<|[""'`]" + Regex.Escape s + @"[""'`]")) then
              yield refusal "SECTION_MISSING" (sprintf "investigation section %s (UI-029) appears in no source" s)
      // The UI-016 palette is defined as theme tokens.
      if not (css |> List.exists (fun f -> isTheme c f.Path)) then
          yield refusal "THEME_MISSING" (sprintf "no theme file (%s) defines the Causa tokens" (String.concat ", " c.ThemeFiles))
      for role, hex in c.Palette do
          if not (themeDefs |> List.exists (fun (_, v) -> v.ToUpperInvariant().Contains hex)) then
              yield refusal "PALETTE_TOKEN_MISSING" (sprintf "palette %s %s (UI-016) is not defined as a theme token" role hex)
      // The theme boundary: literal colours only in custom-property definitions of theme files.
      for f in css do
          let text = stripComments f.Text
          for m in declaration.Matches text do
              let prop, value = m.Groups["prop"].Value, withoutFallbacks m.Groups["value"].Value
              if literalColour.IsMatch value && not (prop.StartsWith "--" && isTheme c f.Path) then
                  yield refusal "LITERAL_COLOUR_OUTSIDE_THEME" (sprintf "%s:%d %s: %s uses a literal colour outside the theme boundary" f.Path (lineOf text m.Index) prop (value.Trim()))
          for m in varUse.Matches text do
              let name = m.Groups["name"].Value
              if not (themeTokens.Contains name || c.FormaProperties.Contains name) then
                  yield refusal "TOKEN_UNDEFINED" (sprintf "%s:%d var(%s) is neither a theme token nor a pinned Forma property" f.Path (lineOf text m.Index) name)
      for f in html do
          for m in Regex.Matches(f.Text, @"style\s*=\s*""([^""]*)""") do
              if literalColour.IsMatch(withoutFallbacks m.Groups[1].Value) then
                  yield refusal "LITERAL_COLOUR_OUTSIDE_THEME" (sprintf "%s:%d an inline style uses a literal colour" f.Path (lineOf f.Text m.Index)) ]
    |> List.distinct

/// Combines the static refusals with the rendered verdict (verification/design-contract, `indy-init.design-rendered/1`).
/// `rendered`: one verdict per engine; every required engine must have one, for this candidate.
let verdictJson (candidate: string) (staticRefusals: Refusal list) (requiredEngines: string list) (rendered: Json list) : Json =
    let renderedCauses =
        [ if rendered.IsEmpty then yield "rendered: the rendered design contract did not run (layout, overflow, touch targets, focus, contrast, screenshots)"
          for engine in requiredEngines do
              if not (rendered |> List.exists (fun r -> strOpt "engine" r = Some engine)) then yield sprintf "rendered: no rendered verdict for %s" engine
          for r in rendered do
              let engine = strOpt "engine" r |> Option.defaultValue "(unknown engine)"
              if strOpt "candidate" r <> Some candidate then yield sprintf "rendered (%s): the rendered verdict is for %s, not %s" engine (strOpt "candidate" r |> Option.defaultValue "(none)") candidate
              for f in arr "failures" r do yield sprintf "rendered %s (%s) at %s: %s" (str "check" f) engine (strOpt "viewport" f |> Option.defaultValue "-") (str "detail" f) ]
    let causes = (staticRefusals |> List.map (fun r -> sprintf "%s %s" r.Code r.Detail)) @ renderedCauses
    JObj
        [ "schema", JStr "indy-init.design-contract/1"
          "candidate", JStr candidate
          "verdict", JStr(if causes.IsEmpty then "PASS" else "REFUSED")
          "static", JArr(staticRefusals |> List.map refusalJson)
          "screenshots", JArr(rendered |> List.collect (arr "screenshots"))
          "independentVisualReview", JStr "required (UI-016-O16): the owner reviews the screenshots; this verdict is not that review"
          "causes", jstrs causes ]
