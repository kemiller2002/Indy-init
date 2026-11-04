# Next rehearsal: agent startup order

This replaces the T0 commands section of `docs/rehearsal-009-preflight-smoke/FINAL-CORRECTIONS.md` for the next rehearsal. That document stays as the Rehearsal 009 record.

`INDY` is an Indy-init checkout at the commit that carries this repair. `LOCK` is the owner's lock for the rehearsal; see the blockers in [FINAL-REPORT.md](FINAL-REPORT.md) §8.

**Order matters.** Implementation starts only after step 4 has been committed. Every implementation commit before it is reported as a process violation, by CI (`check-sequencing` in the Verification gate) and in the final report.

## 1. Before T0: workstation and preflights

```bash
dotnet fsi $INDY/tools/r9/r9.fsx -- install-conditor --lock $LOCK --dir ~/.local/share/indy-init-r9/conditor
CONDITOR=~/.local/share/indy-init-r9/conditor/conditor
dotnet fsi $INDY/tools/r9/r9.fsx -- workstation --lock $LOCK --conditor $CONDITOR --home ~ --target-id indy-init-r9-workstation
dotnet fsi $INDY/tools/r9/r9.fsx -- spec-preflight --indy-init $INDY --lock $LOCK --percepta ~/.local/bin/percepta --out ~/preflight/spec.json
$CONDITOR acceptance preflight --target $INDY | tee ~/preflight/acceptance.log      # must read READY
```

## 2. T0: bootstrap (record the timed start first)

The package is the one the lock pins. Use v4.1.4 once the owner has approved and tagged it (ODR-RG-02). The v4.1.2 runners cannot execute.

```bash
$CONDITOR init --preset indy-init --target .
dotnet $INDY/tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll v41 --source $INDY \
  --package $INDY/requirements-compiled/v4.1.4 --base $INDY/requirements-compiled/v4 --bootstrap .   # writes playwright.config.ts
dotnet fsi $INDY/tools/r9/r9.fsx -- govern --indy-init $INDY --target .
dotnet fsi $INDY/tools/r9/r9.fsx -- check-governance --indy-init $INDY --target .
dotnet build Causa.slnx && $CONDITOR ci-policy check --target .
git add -A && git commit -m "T0 bootstrap"
```

## 3. Initialization: requirements, work items, links, executable matrix

1. **Compile the catalog manifest** from the pinned catalog, not from the application:

   ```bash
   praxis requirements manifest compile --catalog CATALOG --source-commit <Indy-init commit> \
     --mappings requirements/requirement-implementation-map.json --output requirements/verification/verification-manifest.json
   ```

2. **Compile the frozen-plan obligations:**

   ```bash
   dokimos verification compile --indy-execution-plan $INDY/requirements-compiled/v4.1.4/verification-execution-plan.json \
     --indy-verification-plan $INDY/requirements-compiled/v4/verification-plan.json --out .echelon/obligations.json
   ```

3. **Create the Praxis work items** from the authoritative scope (docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md §2): generate `.echelon/r9-work-plan.json` with `requirements-compiler scope work-plan --manifest $INDY/docs/rehearsal-010-readiness/scope-manifest.json --out .echelon/r9-work-plan.json`, begin each of its work items with `./praxis work begin --id ID --occurred-at NOW`, then write and commit the scope checkpoint (`requirements-compiler scope checkpoint`); `check-sequencing --scope-manifest` requires it. The work plan (schema `indy-init.r9-work-plan/1`) lists, for each work item:
   - its `requirements`, as catalog `INV-…` or plan requirement IDs;
   - its `obligations`, as catalog obligation IDs such as `INV-024/V3/enter`, or plan obligation IDs such as `PERSIST-007-O1`.

   Every in-scope obligation must be linked. The scope is the owner-approved competition scope. Until the owner approves one, it is the whole frozen plan plus the catalog: see `competition-scope.proposed.json` and ODR-RG-01.

4. **Check that every suite command can execute:**

   ```bash
   dotnet fsi $INDY/tools/r9/r9.fsx -- suite-preflight --obligations .echelon/obligations.json --root . --out ~/preflight/suites.json
   ```

## 4. Authorize implementation (the initialization checkpoint)

```bash
dotnet fsi $INDY/tools/r9/r9.fsx -- authorize-implementation --target . --catalog CATALOG --obligations .echelon/obligations.json \
  --suite-preflight ~/preflight/suites.json --spec-preflight ~/preflight/spec.json --acceptance-preflight ~/preflight/acceptance.log \
  [--scope APPROVED-SCOPE.json]
git add .echelon/r9-initialization-checkpoint.json && git commit -m "Initialization checkpoint: implementation authorized"
```

The command refuses until every condition holds, in this order:
1. Conditor has initialized the repository.
2. The manifest is current, and Praxis validates.
3. The work items exist.
4. Every in-scope obligation is linked.
5. Every bound suite can execute.
6. The preflights passed.

It does not require verification evidence; that is produced after implementation. The checkpoint is immutable: CI refuses any later change to it.

## 5. Implementation and work-item completion

These are unchanged (FINAL-CORRECTIONS.md): complete each work item with its gate matrix. `check-governance` and `check-sequencing` run on every push and pull request.

## 6. Release: report each acceptance level separately

1. Run the Verification gate with `mode=release` at the candidate.
2. Run the Requirement verification workflow (the catalog) at the same candidate.
3. Then run:

   ```bash
   dotnet fsi $INDY/tools/r9/r9.fsx -- acceptance-status --candidate SHA --catalog-verification requirement-verification.json \
     --gate-dir GATE-OUTPUT --scope APPROVED-SCOPE.json --obligations .echelon/obligations.json --approvals PINNED-APPROVALS \
     [--owner-verdict REPORT] --out evidence/acceptance-status.json
   ```

The final report states all four levels exactly as computed:
- **Catalog:** the Dokimos verdict on the catalog.
- **Competition scope:** an owner-approved scope, verified at the candidate. A scope without an owner approval is SCOPE-NOT-APPROVED.
- **Full plan:** the release gate's verdict on the whole frozen plan.
- **Owner verdict:** the output of `tools/compute-acceptance-verdict.py`, or NOT-COMPUTED.

Catalog acceptance is never reported as release acceptance. A scoped release never claims the full plan.
