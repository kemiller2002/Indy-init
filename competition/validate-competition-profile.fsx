open System
open System.IO
open System.Text.Json

let require condition message =
    if not condition then failwith message

let parse path = JsonDocument.Parse(File.ReadAllBytes path)
let property (name: string) (element: JsonElement) = element.GetProperty name
let str name element = (property name element).GetString()
let array name element = (property name element).EnumerateArray() |> Seq.toArray

use kickoffDoc = parse "kickoff/indy-init.kickoff.json"
use environmentDoc = parse "competition/indy-init.environment.json"
use sourceDoc = parse "competition/indy-init-contracts.source.json"

let kickoff = kickoffDoc.RootElement
let environment = environmentDoc.RootElement
let source = sourceDoc.RootElement

require (str "schema" environment = "indy-init.environment-profile/v1") "environment schema mismatch"
require (str "id" environment = "indy-init") "environment id mismatch"
require (str "status" environment = "pre-freeze") "initial environment must remain pre-freeze until exact event release set exists"

let primaryPlatform = str "primaryPlatform" environment
let supported = array "supportedPlatforms" environment |> Array.map (fun x -> x.GetString())
require (supported |> Array.contains primaryPlatform) "primary platform must be in supportedPlatforms"

let components = array "components" environment
let ids = components |> Array.map (str "systemId")
require (ids.Length = (ids |> Array.distinct |> Array.length)) "environment contains duplicate canonical system ids"
require (not (ids |> Array.contains "ros")) "ros is a Praxis alias and must not be a separate environment component"

let requiredEnvironment =
    components
    |> Array.filter (fun x -> (property "required" x).GetBoolean())
    |> Array.map (str "systemId")
    |> Set.ofArray

let normalizeKickoffId id =
    match id with
    | "ros" -> "praxis"
    | other -> other

let requiredKickoff =
    array "components" kickoff
    |> Array.filter (fun x -> (property "required" x).GetBoolean())
    |> Array.map (str "id")
    |> Array.map normalizeKickoffId
    |> Set.ofArray

let missing = Set.difference requiredKickoff requiredEnvironment
require missing.IsEmpty $"environment profile is missing required kickoff components: {String.concat ", " missing}"

let praxis =
    components |> Array.find (fun x -> str "systemId" x = "praxis")

let praxisPurpose =
    array "purpose" praxis |> Array.map (fun x -> x.GetString())

require (praxisPurpose |> Array.exists (fun p -> p.Contains("formerly described as ROS", StringComparison.Ordinal))) "Praxis component must explicitly preserve the historical ROS execution purpose"
require ((property "applicationImplementationIncluded" (property "implementationBoundary" environment)).GetBoolean() = false) "environment must not contain application implementation"
require ((property "applicationImplementationIncluded" source).GetBoolean() = false) "contract bundle source must not contain application implementation"

let sourcePaths = array "files" source |> Array.map (str "path") |> Set.ofArray
let normativePaths =
    array "normativeDocuments" kickoff
    |> Array.map (str "path")
    |> Set.ofArray

let missingNormative = Set.difference normativePaths sourcePaths
require missingNormative.IsEmpty $"contract bundle omits kickoff normative documents: {String.concat ", " missingNormative}"

let boundary = str "boundaryDocument" (property "planningBaseline" kickoff)
require (sourcePaths.Contains boundary) "contract bundle omits hackathon boundary document"
require (sourcePaths.Contains "kickoff/indy-init.kickoff.json") "contract bundle omits kickoff manifest"
require (sourcePaths.Contains "kickoff/kickoff-manifest.schema.json") "contract bundle omits kickoff schema"

printfn "Indy Init competition profile PASS"
printfn "  primary platform: %s" primaryPlatform
printfn "  required canonical systems: %s" (requiredEnvironment |> Set.toList |> String.concat ", ")
printfn "  contract source files: %d" sourcePaths.Count
