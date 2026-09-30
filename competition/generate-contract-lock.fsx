open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

let require condition message =
    if not condition then failwith message

let parse path = JsonDocument.Parse(File.ReadAllBytes path)

let property (name: string) (element: JsonElement) = element.GetProperty name
let str name element = (property name element).GetString()
let array name element = (property name element).EnumerateArray() |> Seq.toArray

let hex bytes = Convert.ToHexString(bytes).ToLowerInvariant()

let sha256Bytes (bytes: byte array) =
    SHA256.HashData bytes |> hex

let gitBlobSha1 (bytes: byte array) =
    let header = Encoding.UTF8.GetBytes($"blob {bytes.Length}\000")
    let payload = Array.zeroCreate<byte> (header.Length + bytes.Length)
    Buffer.BlockCopy(header, 0, payload, 0, header.Length)
    Buffer.BlockCopy(bytes, 0, payload, header.Length, bytes.Length)
    SHA1.HashData payload |> hex

let sourcePath =
    if fsi.CommandLineArgs.Length > 1 then fsi.CommandLineArgs[1]
    else "competition/indy-init-contracts.source.json"

let outputPath =
    if fsi.CommandLineArgs.Length > 2 then fsi.CommandLineArgs[2]
    else "competition/indy-init-contracts.lock.json"

use sourceDoc = parse sourcePath
let source = sourceDoc.RootElement

require (str "schema" source = "indy-init.contract-bundle-source/v1") "bundle source schema mismatch"
require ((property "applicationImplementationIncluded" source).GetBoolean() = false) "contract bundle must not include application implementation"
require (str "inclusionPolicy" source = "explicit-whitelist-only") "contract bundle must use explicit whitelist inclusion"

let files = array "files" source
let paths = files |> Array.map (str "path")
require (paths.Length = (paths |> Array.distinct |> Array.length)) "contract bundle contains duplicate paths"

let locked = JsonArray()
let canonical = ResizeArray<string>()

for item in files do
    let path = str "path" item
    let expectedBlob = str "gitBlobSha1" item
    let role = str "role" item

    require (not (Path.IsPathRooted path)) $"contract path must be relative: {path}"
    require (not (path.Split([| '/'; '\\' |], StringSplitOptions.RemoveEmptyEntries) |> Array.contains "..")) $"contract path escapes repository: {path}"
    require (File.Exists path) $"contract path does not exist: {path}"

    let bytes = File.ReadAllBytes path
    let actualBlob = gitBlobSha1 bytes
    require (actualBlob = expectedBlob) $"Git blob identity changed for {path}: expected {expectedBlob}, observed {actualBlob}"

    let digest = sha256Bytes bytes
    canonical.Add($"{path}|{digest}")

    let node = JsonObject()
    node["path"] <- JsonValue.Create path
    node["role"] <- JsonValue.Create role
    node["gitBlobSha1"] <- JsonValue.Create actualBlob
    node["sha256"] <- JsonValue.Create digest
    node["size"] <- JsonValue.Create bytes.Length
    locked.Add node

let canonicalBytes =
    canonical
    |> Seq.sort
    |> String.concat "\n"
    |> Encoding.UTF8.GetBytes

let bundleDigest = sha256Bytes canonicalBytes

let output = JsonObject()
output["schema"] <- JsonValue.Create "indy-init.contract-bundle-lock/v1"
output["id"] <- JsonValue.Create(str "id" source)
output["version"] <- JsonValue.Create(str "version" source)
output["repository"] <- JsonValue.Create(str "repository" source)
output["sourceCommit"] <- JsonValue.Create(str "sourceCommit" source)
output["inclusionPolicy"] <- JsonValue.Create "explicit-whitelist-only"
output["applicationImplementationIncluded"] <- JsonValue.Create false
output["bundleSha256"] <- JsonValue.Create bundleDigest
output["files"] <- locked

let options = JsonSerializerOptions(WriteIndented = true)
File.WriteAllText(outputPath, output.ToJsonString(options) + "\n")

printfn "Contract bundle lock PASS"
printfn "bundleSha256=%s" bundleDigest
printfn "files=%d" files.Length
printfn "--- LOCK BEGIN ---"
printf "%s" (File.ReadAllText outputPath)
printfn "--- LOCK END ---"
