/// FILE-OWNERSHIP.json: one owner per file, every proposed UI slice has files of its own role, and no UI role owns a
/// path the frozen rules reserve to integration (project-structure.json rules, checks X02 and X18).
module Causa.Ui.Tests.OwnershipTests

open Xunit
open Causa.Ui.Tests.Support

let ownership = proposal "FILE-OWNERSHIP.json"
let files = arr ownership "files"

[<Fact>]
let ``every path has exactly one owner`` () =
    let paths = files |> List.choose (fun f -> str f "path")
    Assert.Equal(paths.Length, (List.distinct paths).Length)
    Assert.All(files, fun f -> Assert.False(System.String.IsNullOrWhiteSpace(str f "owner" |> Option.defaultValue "")))

[<Fact>]
let ``every UI slice of each proposed role map is implemented by files of the same role`` () =
    for rolesFile in [ "schedule-inputs/roles.contract-first-3ui.json" ] do
        let roles = (proposal rolesFile).GetProperty("roles").EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value.GetString() |> string) |> List.ofSeq
        for (slice, role) in roles do
            let owners = files |> List.filter (fun f -> strs f "slices" |> List.contains slice) |> List.choose (fun f -> str f "owner")
            Assert.True(not owners.IsEmpty, sprintf "%s has no file" slice)
            Assert.True(owners |> List.forall ((=) role), sprintf "%s (%s) is implemented by files owned by %A" slice role owners)

[<Fact>]
let ``no UI role owns a path the frozen rules reserve to integration`` () =
    let reserved (p: string) =
        p.EndsWith ".fsproj" || p.EndsWith ".slnx" || p.StartsWith ".github/" || p = "web/index.html" || p = "web/causa.css"
        || p = "src/Causa.App/View.fs" || p = "src/Causa.App/Program.fs" || p.StartsWith "src/Causa.Host/" || p = "package.json" || p = "build.fsx"
    for f in files do
        let p = str f "path" |> Option.get
        if reserved p then Assert.Equal(Some "integration", str f "owner")

[<Fact>]
let ``no two UI roles own the same view or screen fragment`` () =
    let uiOwned = files |> List.filter (fun f -> (str f "owner" |> Option.defaultValue "").StartsWith "ui-") |> List.choose (fun f -> str f "path")
    Assert.Equal(uiOwned.Length, (List.distinct uiOwned).Length)
