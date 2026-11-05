# Causa preview

**A working preview of Causa:** Connect → Browse → Open → Create → Save → Reload, on the Echelon Foundry stack.
- **F# engine:** compiled to WebAssembly.
- **Limen 0.9.1:** the browser kernel; every browser effect crosses as data.
- **Forma 0.10.0:** the UI.
- **The real historical records:** read from `kemiller2002/causa-data`.

**F# only:** see [ARCHITECTURE.md](ARCHITECTURE.md) for the two documented non-F# exceptions and the check that enforces them.

**This is not the competition build and is not copied into it** (OD-INDY-22; the event rule "code starts at kickoff"). It exists so the owner can judge a running application before the event. Its decisions are in [DECISIONS.md](DECISIONS.md).

## What each screen shows, honestly

| Level | Shown as |
|---|---|
| Raw data | the line count and the commit SHA the three files were read at |
| Decoded events | "N of N lines decoded to typed events (none dropped)", or a fail-closed refusal with the CONTRACT §11.1 code |
| Replayed state | the §8.3 fold, equal to `snapshot.json` byte for byte, plus a check against the catalog manifest's `expectedStateSha256` |
| Authoritative state | **never claimed**: "Admissibility not evaluated" (CONTRACT §11.3) |

## Build, run, test

```bash
cd preview/causa/web && npm ci && cd ..
dotnet fsi build.fsx                      # dist/: WebAssembly app + Limen + Forma (about 1 min)
dotnet fsi serve.fsx 4173                 # http://localhost:4173/
dotnet fsi tests/ArchitectureCheck.fsx                                  # F#-only constraint (A1-A7)
dotnet run --project tests/Engine.Tests -- <causa-data checkout>          # 31 engine tests, in-process GitHub double
NODE_PATH=$(npm root -g) node tests/read-path.mjs http://localhost:4173/  # browser, real causa-data, mobile viewport
CAUSA_TOKEN=… CAUSA_WRITE_REPO=owner/name node tests/real-provider.mjs http://localhost:4173/   # owner-run, real writes
```
