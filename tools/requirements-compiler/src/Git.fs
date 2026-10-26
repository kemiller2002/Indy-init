/// Read-only access to git checkouts. Content is always read at an explicit commit,
/// never from the working tree, so a compilation depends only on the recorded revisions.
module Git

open System.Diagnostics
open System.IO
open System.Security.Cryptography

let run (repo: string) (args: string list) : Result<byte[], string> =
    let psi = ProcessStartInfo("git")
    [ "-C"; repo ] @ args |> List.iter psi.ArgumentList.Add
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    use p = Process.Start psi
    use ms = new MemoryStream()
    let errTask = p.StandardError.ReadToEndAsync()
    p.StandardOutput.BaseStream.CopyTo ms
    p.WaitForExit()
    if p.ExitCode = 0 then Ok(ms.ToArray()) else Error(errTask.Result.Trim())

let headCommit repo =
    run repo [ "rev-parse"; "HEAD" ] |> Result.map (fun b -> System.Text.Encoding.ASCII.GetString(b).Trim())

let hasCommit repo commit =
    match run repo [ "cat-file"; "-t"; commit ] with
    | Ok _ -> true
    | Error _ -> false

let showFile repo commit path = run repo [ "show"; sprintf "%s:%s" commit path ]

let sha256Hex (bytes: byte[]) =
    SHA256.HashData(bytes) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

let listFiles repo commit =
    run repo [ "ls-tree"; "-r"; "--name-only"; commit ]
    |> Result.map (fun b -> System.Text.Encoding.UTF8.GetString(b).Split('\n', System.StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
