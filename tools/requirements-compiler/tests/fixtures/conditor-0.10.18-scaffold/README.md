# Recorded Conditor 0.10.18 scaffold (test fixture)

The files that `conditor init --preset indy-init --target .` (Conditor 0.10.18 release binary `conditor-linux-x64`, sha256 `298f5df4…eecc03`) writes into an empty repository, recorded on 2026-11-01. Only the scaffold files the v4.1.1 bootstrap meets are kept:

- the files it merges: `package.json`, `Directory.Build.props`, and `.gitignore`, stored here as `gitignore.txt` so it does not apply to this folder;
- `conditor.json`, which marks the repository as Conditor's;
- Conditor's own solution and projects (`App.slnx`, `src/engine`, `tests/App.Engine.Tests`), which must still build after the layering.

CI copies them into a git repository, layers the v4.1.1 skeleton onto them with `requirements-compiler v41 --bootstrap`, and builds both solutions. The offline suite layers onto them too. This is a test fixture, not application source: it is Conditor's generic scaffold, not Causa code.
