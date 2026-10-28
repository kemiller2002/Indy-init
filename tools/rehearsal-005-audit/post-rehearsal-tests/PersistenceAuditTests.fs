/// Post-rehearsal tests written by the Rehearsal 5 implementation audit. Each test names the frozen obligation it
/// examines in an `audit` trait. They exercise the frozen candidate's own code through its own harness; they are
/// audit evidence recorded after the rehearsal, not obligations bound or passed in run 40620099844.
module PostRehearsal.Tests.PersistenceAuditTests

open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic
open Persistence.Tests.Harness

let private utf8Text (s: string) = System.Text.Encoding.UTF8.GetBytes s

let private codeOf (r: Result<'T, ContractError>) =
    match r with
    | Ok _ -> "OK"
    | Error e -> e.Code

let private isOk r = Result.isOk r
let private isRefused r = Result.isError r

let private pointer (manifest: string) (workspace: string) =
    utf8Text (sprintf "{\"activeCatalog\":\"a\",\"catalogId\":\"c\",\"catalogVersion\":\"1.0.0\",\"manifest\":\"%s\",\"workspace\":\"%s\"}\n" manifest workspace)

let private validPointer =
    match Catalog.parsePointer (pointer "catalog/historical-v1/manifest.json" "workspaces/demo/historical-v1") with
    | Ok p -> p
    | Error e -> failwithf "%s %s" e.Code e.Detail

let private digest = String.replicate 64 "a"

let private caseJson (id: string) (path: string) =
    sprintf
        "{\"caseKey\":\"k\",\"investigationId\":\"%s\",\"title\":\"t\",\"path\":\"%s\",\"expectedVersion\":1,\"expectedLifecycle\":\"active\",\"expectedStateSha256\":\"%s\",\"eventsSha256\":\"%s\",\"manifestSha256\":\"%s\",\"snapshotSha256\":\"%s\"}"
        id path digest digest digest digest

let private manifestJson (workspace: string) (formatVersion: string) (cases: string list) =
    utf8Text (sprintf "{\"workspace\":\"%s\",\"formatVersion\":\"%s\",\"cases\":[%s]}\n" workspace formatVersion (String.concat "," cases))

let private uuid = id "audit-case"
let private ws = "workspaces/demo/historical-v1"
let private inWs = ws + "/investigations/" + uuid

// ---- EXT-DATA-005-O1: file paths and manifests are validated before anything loads ---------------------------

[<Theory>]
[<Trait("audit", "EXT-DATA-005-O1")>]
[<InlineData("catalog/../secrets/manifest.json", "workspaces/demo/historical-v1")>]
[<InlineData("/catalog/historical-v1/manifest.json", "workspaces/demo/historical-v1")>]
[<InlineData("catalog/historical-v1/other.json", "workspaces/demo/historical-v1")>]
[<InlineData("catalog/historical-v1/manifest.json", "workspaces/demo/../../etc")>]
[<InlineData("catalog/historical-v1/manifest.json", "workspaces/private/historical-v1")>]
[<InlineData("catalog/Historical-V1/manifest.json", "workspaces/demo/historical-v1")>]
let ``a catalog pointer naming a path outside the catalog or demo workspaces is refused`` (manifest: string, workspace: string) =
    Assert.Equal(Codes.schemaViolation, codeOf (Catalog.parsePointer (pointer manifest workspace)))

[<Fact>]
[<Trait("audit", "EXT-DATA-005-O1")>]
let ``a well-formed pointer and manifest parse, so the refusals below are not vacuous`` () =
    Assert.True(isOk (Catalog.parseManifest validPointer (manifestJson ws "1.0.0" [ caseJson uuid inWs ])))

[<Fact>]
[<Trait("audit", "EXT-DATA-005-O1")>]
let ``a manifest that is not schema-compatible or not consistent with its pointer is refused`` () =
    let refusals =
        [ "workspace differs", manifestJson "workspaces/demo/other" "1.0.0" [ caseJson uuid inWs ], Codes.schemaViolation
          "future format", manifestJson ws "2.0.0" [ caseJson uuid inWs ], Codes.unsupportedVersion
          "case outside workspace", manifestJson ws "1.0.0" [ caseJson uuid ("workspaces/demo/other/investigations/" + uuid) ], Codes.schemaViolation
          "case id not a UUID", manifestJson ws "1.0.0" [ caseJson "apollo-1" (ws + "/investigations/apollo-1") ], Codes.schemaViolation
          "same case twice", manifestJson ws "1.0.0" [ caseJson uuid inWs; caseJson uuid inWs ], Codes.schemaViolation
          "no cases", manifestJson ws "1.0.0" [], Codes.schemaViolation
          "not JSON", utf8Text "{cases\n", Codes.jsonMalformed ]
    refusals
    |> List.iter (fun (name, bytes, expected) ->
        let got = codeOf (Catalog.parseManifest validPointer bytes)
        Assert.True((got = expected), sprintf "%s: expected %s, got %s" name expected got))

[<Fact>]
[<Trait("audit", "EXT-DATA-005-O1")>]
let ``a repository the credential cannot see connects as access denied and loads nothing`` () =
    let privateRepo = Persistence.Tests.FakeGitHub.Fake(owner, repo, branch, false)
    let app = App(privateRepo)
    app.Connect "audit-denied" |> ignore
    Assert.Equal("accessDenied", app.Connection)
    Assert.True(app.Model.Documents.IsEmpty)
    Assert.True(privateRepo.Requests |> List.forall (fun r -> r.Method = "GET"))

// ---- PF-009-O3: request identity, active provider target, credential generation, freshness -------------------

[<Fact>]
[<Trait("audit", "PF-009-O3")>]
let ``an answer for the previously active target never lands after the user switches workspace`` () =
    let fake = newFake ()
    let app = App(fake)
    app.Faults <- (fun _ -> Hold)
    Assert.True(isOk (app.Connect "audit-target-a"))
    let heldForA, _ = app.Held.Head
    app.Faults <- (fun _ -> Deliver)
    Assert.True(isOk (app.Connect "audit-target-b"))
    let before = app.Model
    Assert.True(isRefused (app.DeliverHeld heldForA))
    Assert.Equal(before, app.Model)
    Assert.Equal(Some "audit-target-b", app.Model.Target |> Option.map (fun t -> t.Workspace))

[<Fact>]
[<Trait("audit", "PF-009-O3")>]
let ``an answer issued under a credential that was later rejected is refused and the rejection is not masked`` () =
    let fake = newFake ()
    let app = App(fake)
    app.Faults <- (fun _ -> Hold)
    Assert.True(isOk (app.Connect "audit-generation"))
    let heldUnderGood, _ = app.Held.Head
    let generationBefore = app.Model.Generation
    app.Faults <- (fun _ -> Deliver)
    app.Connect("audit-generation", "causa-revoked-token-0000") |> ignore
    Assert.Equal("accessDenied", app.Connection)
    Assert.True(app.Model.Generation > generationBefore)
    Assert.Equal(None, app.Model.Credential)
    let afterRejection = app.Model
    Assert.True(isRefused (app.DeliverHeld heldUnderGood))
    Assert.Equal(afterRejection, app.Model)
    Assert.Equal("accessDenied", app.Connection)

[<Fact>]
[<Trait("audit", "PF-009-O3")>]
let ``every connect and disconnect starts a new generation`` () =
    let app = App(newFake ())
    let generations =
        [ (fun () -> app.Connect "audit-gen-1")
          (fun () -> app.Step Disconnect)
          (fun () -> app.Connect "audit-gen-2") ]
        |> List.map (fun step -> step () |> ignore; app.Model.Generation)
    Assert.Equal<int list>(generations |> List.sort |> List.distinct, generations)

// ---- PF-015-O1: characterisation of the open limit F-3 (no anonymous read path) -------------------------------

[<Fact>]
[<Trait("audit", "PF-015-O1")>]
let ``characterisation: the application has no anonymous path, a blank credential is refused before any request`` () =
    // This test passes because the gap exists. It documents F-3; it is not evidence that PF-015-O1 is met.
    Assert.True(Result.isError (Credential.create ""))
    Assert.True(Result.isError (Credential.create "   "))
