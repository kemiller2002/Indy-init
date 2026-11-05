// Practice only (FENCE.md). Engine tests: real causa-data through every layer, Create -> files -> read-back, and the
// whole engine flow against an in-memory provider. The in-memory provider proves component behavior only.
//
//   dotnet run --project tests/Causa.Practice.Tests -- --data <causa-data clone>
open System
open System.IO
open System.Text
open System.Text.Json.Nodes
open Causa.HistoricalCompatibility
open Causa.Practice

let args = Environment.GetCommandLineArgs() |> List.ofArray
let rec opt k = function a :: v :: _ when a = k -> Some v | _ :: t -> opt k t | [] -> None
let dataDir = opt "--data" args |> Option.defaultWith (fun () -> failwith "--data <causa-data clone> is required")
let workspace = Path.Combine(dataDir, "workspaces/demo/demonstration-v1/investigations")

let rawOf (dir: string) = { Directory = Path.GetFileName dir; Files = Engine.fileNames |> List.map (fun n -> n, File.ReadAllBytes(Path.Combine(dir, n))) }

type Case = { Name: string; Run: unit -> Result<unit, string> }
let case name run = { Name = name; Run = run }
let check cond msg = if cond then Ok() else Error msg

// ---- an in-memory GitHub contents API, driven through the engine's own requests ----
type Store = Map<string, byte[]> // "owner/repo/branch/path" -> bytes

let respond (store: Store) (r: HttpRequest) : Store * HttpOutcome =
    let u = Uri r.Url
    let parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) |> Array.map Uri.UnescapeDataString |> List.ofArray
    let q = Web.HttpUtility.ParseQueryString u.Query
    match r.Method, parts with
    | "GET", [ "repos"; o; rp ] -> store, Responded(200, JsonNode.Parse $"""{{"full_name":"{o}/{rp}"}}""")
    | "GET", "repos" :: o :: rp :: "contents" :: path ->
        let key = String.Join("/", [ o; rp; q["ref"] ] @ path)
        match store.TryFind key with
        | Some bytes -> store, Responded(200, JsonNode.Parse $"""{{"content":"{Convert.ToBase64String bytes}","encoding":"base64"}}""")
        | None ->
            let names = store |> Map.toList |> List.choose (fun (k, _) -> if k.StartsWith(key + "/") then Some(k.Substring(key.Length + 1).Split('/')[0]) else None) |> List.distinct
            if names.IsEmpty then store, Responded(404, JsonNode.Parse """{"message":"Not Found"}""")
            else store, Responded(200, JsonArray(names |> List.map (fun n -> JsonNode.Parse $"""{{"name":"{n}","type":"dir"}}""") |> Array.ofList))
    | "PUT", "repos" :: o :: rp :: "contents" :: path ->
        let body = JsonNode.Parse(r.Body.Value)
        if r.Headers |> List.exists (fun (k, _) -> k = "Authorization") |> not then store, Responded(401, JsonNode.Parse """{"message":"Requires authentication"}""")
        else
            let key = String.Join("/", [ o; rp; body["branch"].GetValue<string>() ] @ path)
            if store.ContainsKey key then store, Responded(422, JsonNode.Parse """{"message":"sha wasn't supplied"}""")
            else store.Add(key, Convert.FromBase64String(body["content"].GetValue<string>())), Responded(201, JsonObject())
    | _ -> store, Responded(404, JsonObject())

/// Runs the engine until no effect is outstanding. `tamper` may corrupt a GET answer to make a negative case.
let rec drive (ctx: Context) (tamper: HttpRequest -> HttpOutcome -> HttpOutcome) (store: Store, m: Model) (effects: (string * HttpRequest) list) =
    match effects with
    | [] -> store, m
    | (id, r) :: rest ->
        let store', o = respond store r
        let m', more = Engine.update ctx m (HttpDone(id, tamper r o))
        drive ctx tamper (store', m') (rest @ more)

let ctx = { Now = DateTimeOffset.Parse "2026-11-05T20:00:00.123Z"; Fresh = Guid.NewGuid }
let events (m: Model) names = names |> List.fold (fun (m, fx) (n, k, v) -> let m', f = Engine.update ctx m (Event(n, k, v)) in m', fx @ f) (m, [])
let seed () =
    Directory.GetDirectories workspace
    |> Array.collect (fun d -> Engine.fileNames |> List.map (fun n -> $"kemiller2002/causa-data/main/workspaces/demo/demonstration-v1/investigations/{Path.GetFileName d}/{n}", File.ReadAllBytes(Path.Combine(d, n))) |> Array.ofList)
    |> Map.ofArray
let connectWith (token: string) (store: Store) tamper =
    let m, fx = events Engine.initial [ "draft:api", None, Some "http://localhost:9"; "draft:token", None, Some token; "connect", None, None ]
    drive ctx tamper (store, m) fx
let noTamper _ o = o
let createIn (store, m) tamper =
    let m, fx = events m [ "showCreate", None, None; "draft:title", None, Some "Practice: pump trip"; "draft:original", None, Some "Pump P-101 tripped at 14:02."; "create", None, None ]
    drive ctx tamper (store, m) fx
let viewOf m = View.project m

let cases =
    [ case "real causa-data: every investigation is raw-read, decoded, replayed, and its snapshot agrees" (fun () ->
          let dirs = Directory.GetDirectories workspace |> Array.sort
          let bad = dirs |> Array.map (rawOf >> Layers.load) |> Array.filter (Layers.isVerified >> not)
          check (dirs.Length = 4 && bad.Length = 0) $"{dirs.Length} dirs, {bad.Length} not verified")
      case "real causa-data: event counts are the catalog's (Apollo 1 = 432, Apollo 11 = 431, Challenger = 452)" (fun () ->
          let count id = match (Layers.load (rawOf (Path.Combine(workspace, id)))).Decoded with Ok d -> d.Read.Events.Length | Error _ -> -1
          check ([ count "c6b26614-fc43-5013-a15c-cf95942ee07b"; count "e3adbee5-5096-5a10-bbfb-cecda86a154b"; count "1784da3c-7141-59ee-a495-1d5d44bf2284" ] = [ 432; 431; 452 ]) "counts differ")
      case "layers stay distinct: a tampered snapshot is decoded and replayed but NOT authoritative" (fun () ->
          let raw = rawOf (Path.Combine(workspace, "c6b26614-fc43-5013-a15c-cf95942ee07b"))
          let snap = raw.Files |> List.find (fst >> (=) "snapshot.json") |> snd |> Encoding.UTF8.GetString
          let tampered = { raw with Files = raw.Files |> List.map (fun (n, b) -> if n = "snapshot.json" then n, Encoding.UTF8.GetBytes(snap.Replace("Apollo 1 (AS-204)", "Apollo 1 (AS-205)")) else n, b) }
          let l = Layers.load tampered
          check (Result.isOk l.Replayed && not (Layers.isVerified l)) "tampered snapshot was accepted")
      case "layers stay distinct: a tampered event line is refused at decoding, nothing is replayed" (fun () ->
          let raw = rawOf (Path.Combine(workspace, "c6b26614-fc43-5013-a15c-cf95942ee07b"))
          let tampered = { raw with Files = raw.Files |> List.map (fun (n, b) -> if n = "events.ndjson" then n, Array.append b [| byte '\n' |] else n, b) }
          let l = Layers.load tampered
          check (Result.isError l.Decoded && Result.isError l.Replayed && Result.isError l.Authoritative) "a tampered history was decoded")
      case "create: the new file set decodes, replays and verifies; its title is the one entered" (fun () ->
          match Create.validate ctx.Now (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) "Valve stuck" "Valve V-7 stuck open." with
          | Error e -> Error(sprintf "%A" e)
          | Ok n ->
              Create.files n
              |> Result.bind (fun files ->
                  let l = Layers.load { Directory = n.InvestigationId.Text; Files = files }
                  check (Layers.isVerified l && View.titleOf l = "Valve stuck") "created files did not verify"))
      case "create: blank title and description are refused per field" (fun () ->
          match Create.validate ctx.Now (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) " " "" with
          | Error errs -> check (List.map fst errs = [ "title"; "original" ]) (sprintf "%A" errs)
          | Ok _ -> Error "blank input accepted")
      case "engine: Connect -> Browse lists and verifies the 4 real investigations (in-memory provider)" (fun () ->
          let _, m = connectWith "" (seed ()) noTamper
          let v = viewOf m
          let rows = v["investigations"].AsArray()
          check (m.Connected && m.Route = Browse && rows.Count = 4 && rows |> Seq.forall (fun r -> r["status"].GetValue<string>().StartsWith "verified")) (v.ToJsonString()))
      case "engine: a listing that names a directory more than once lists it once" (fun () ->
          let store, m = connectWith "" (seed ()) noTamper
          let id = m.Pending |> Map.toList |> List.tryHead
          let m, fx = Engine.update ctx { m with Pending = m.Pending.Add("dup", ListDir) } (HttpDone("dup", Responded(200, JsonNode.Parse """[{"name":"c6b26614-fc43-5013-a15c-cf95942ee07b"},{"name":"c6b26614-fc43-5013-a15c-cf95942ee07b"}]""")))
          ignore (store, id, fx)
          check (m.Listing = Listed [ "c6b26614-fc43-5013-a15c-cf95942ee07b" ]) (sprintf "%A" m.Listing))
      case "engine: Open shows four labelled layers" (fun () ->
          let store, m = connectWith "" (seed ()) noTamper
          let m, fx = events m [ "open", Some "c6b26614-fc43-5013-a15c-cf95942ee07b", None ]
          let _, m = drive ctx noTamper (store, m) fx
          let v = viewOf m
          let t (k: string) = v[k].GetValue<string>()
          check ((t "layer_raw").StartsWith "Raw:" && (t "layer_decoded").StartsWith "Decoded: 432 events" && (t "layer_replayed").StartsWith "Replayed:" && (t "layer_authoritative").Contains "equals the replayed projection" && (t "open_title").StartsWith "Apollo 1") (v.ToJsonString()))
      case "engine: Create -> Save -> read-back verified, and the new investigation is listed" (fun () ->
          let store, m = createIn (connectWith "test-token" (seed ()) noTamper) noTamper
          let dir = match m.Route with Open d -> d | r -> failwithf "route %A" r
          let v = viewOf m
          check (v["readBackVerified"].GetValue<bool>() && (match m.Listing with Listed ds -> List.contains dir ds | _ -> false) && store.Count = 15) (v.ToJsonString()))
      case "engine: Reload (a fresh engine, reconnect) opens the saved investigation verified" (fun () ->
          let store, m = createIn (connectWith "test-token" (seed ()) noTamper) noTamper
          let dir = match m.Route with Open d -> d | _ -> ""
          let store, fresh = connectWith "" store noTamper
          let fresh, fx = events fresh [ "open", Some dir, None ]
          let _, fresh = drive ctx noTamper (store, fresh) fx
          let v = viewOf fresh
          check (v["open_title"].GetValue<string>() = "Practice: pump trip" && v["layer_authoritative"].GetValue<string>().Contains "equals") (v.ToJsonString()))
      case "negative: a read-back whose bytes differ from those written is NOT verified" (fun () ->
          let flip (r: HttpRequest) (o: HttpOutcome) =
              match o with
              | Responded(200, body) when r.Url.Contains "events.ndjson" && r.Url.Contains "Practice" |> not && (body :? JsonObject) && (body["content"] <> null) && r.Url.Contains "demonstration-v1/investigations/" && not (r.Url.Contains "c6b26614" || r.Url.Contains "e3adbee5" || r.Url.Contains "1784da3c" || r.Url.Contains "fc3f71a9") ->
                  Responded(200, JsonNode.Parse $"""{{"content":"{Convert.ToBase64String(Encoding.UTF8.GetBytes "{}\n")}","encoding":"base64"}}""")
              | o -> o
          let _, m = createIn (connectWith "test-token" (seed ()) noTamper) flip
          let v = viewOf m
          check (not (v["readBackVerified"].GetValue<bool>()) && v["readBackStatus"].GetValue<string>().Contains "FAILED") (v.ToJsonString()))
      case "negative: saving without a token is refused before any write" (fun () ->
          let store, m = createIn (connectWith "" (seed ()) noTamper) noTamper
          check (store.Count = 12 && m.Creates.IsEmpty && m.Notice.Contains "token") m.Notice)
      case "negative: a failed write is reported as a failed save, never as saved" (fun () ->
          let store, m = connectWith "test-token" (seed ()) noTamper
          let m, fx = events m [ "showCreate", None, None; "draft:title", None, Some "X"; "draft:original", None, Some "Y"; "create", None, None ]
          let _, m = drive ctx (fun r o -> if r.Method = "PUT" then Responded(403, JsonNode.Parse """{"message":"Resource not accessible by integration"}""") else o) (store, m) fx
          let v = viewOf m
          check (v["saveStatus"].GetValue<string>().StartsWith "Save FAILED" && not (v["readBackVerified"].GetValue<bool>())) (v.ToJsonString()))
      case "security: the token never appears in the projected view or in the draft after connecting" (fun () ->
          let _, m = createIn (connectWith "ghs_SECRET_TOKEN_VALUE" (seed ()) noTamper) noTamper
          check (not ((viewOf m).ToJsonString().Contains "ghs_SECRET") && (m.Draft.["token"] = "")) "token leaked")
      case "protocol: an Event message round-trips through dispatch to a view with no effects" (fun () ->
          let out = Protocol.dispatch DateTimeOffset.UtcNow Guid.NewGuid """{"kind":"Event","event":{"kind":"Event","name":"draft:owner","value":"someone"}}"""
          let o = JsonNode.Parse out
          let view = o["view"]
          let owner = view["draft_owner"].GetValue<string>()
          check (owner = "someone" && o["effects"].AsArray().Count = 0) out) ]

let results =
    cases |> List.map (fun c ->
        let r = try c.Run() with ex -> Error(ex.ToString())
        match r with
        | Ok() -> printfn "PASS %s" c.Name; true
        | Error e -> printfn "FAIL %s\n     %s" c.Name (if e.Length > 600 then e.Substring(0, 600) + "…" else e); false)
let failed = results |> List.filter not |> List.length
printfn "%d/%d passed" (results.Length - failed) results.Length
exit (if failed = 0 then 0 else 1)
