# Echelon System Version and Compatibility Requirements

Status: normative for future Indy-init rehearsals and production builds. Version selection is resolved at build preparation, not assumed from this document.

**VER-001 [P0] Latest compatible verified release.** Each build MUST use the newest available stable, published, compatible release of every applicable Echelon system at the time the build baseline is locked. Applicable systems include Praxis, Ordo, Dokimos, Percepta, Forma, Forma Studio/workflow layout, Limen, Folio, and any actually integrated Echelon dependencies. Discover the real owner and release channel for each; do not invent a release or assume every system is a runtime dependency.

**VER-002 [P0] Verify release freshness.** Query authoritative package registries, GitHub releases/tags, or the owning repository's documented release source immediately before baseline lock. Record checked timestamp, candidate release, source reference, and why it was selected. Do not equate latest main commit with latest released artifact.

**VER-003 [P0] Compatibility over blind upgrades.** Prefer latest stable only when integration contracts and acceptance tests pass. If the newest release is incompatible, fail the upgrade gate or obtain a documented, authorized exception for the newest known compatible release. Never silently fall back to an old version.

**VER-004 [P0] Reproducible lock.** Pin exact package versions and immutable artifact digests/commit SHAs in a machine-readable dependency manifest. The lock is fixed for a given rehearsal/run and must not drift mid-run. Re-resolve latest versions for each new rehearsal or explicitly authorized refresh.

**VER-005 [P0] No unreviewed prereleases.** Do not adopt alpha, beta, RC, unpublished source, or unverified local builds merely because they are newer. Such use requires explicit approval, provenance, and the same compatibility and security gates.

**VER-006 [P0] Cross-system contract validation.** Verify supported API/schema compatibility across Praxis, Ordo, Dokimos, Percepta, Forma, workflow layout, Limen, Folio, and other applicable systems. Exercise integration tests, not just version-string comparisons. Include the local bootstrap/build path and deployed browser where applicable.

**VER-007 [P0] Version observability.** Show resolved application, dependency, and build versions in Administration (UI-018), not investigation pages, catalog cards, or the global footer. Versions must reflect actual loaded components rather than aspirational requirements.

**VER-008 [P0] CI enforcement.** A mandatory gate must verify the locked manifest against installed/loaded artifacts and compatibility evidence. Missing dependencies, stale evidence, unverified artifacts, or skipped gates result in BLOCKED/FAIL, not PASS.

**VER-009 [P1] Upgrade impact and rollback.** Record release notes, breaking changes, migration needs, security advisories, rollback target, and tests affected by each upgrade. Keep historical run manifests and evidence immutable.

**VER-010 [P0] Requirements traceability.** Every future rehearsal must reconcile VER-001..VER-010 with implementation and tests or an explicitly approved exclusion. Work items must go through each owning repository's documented Praxis input path, not GitHub Issues.

## Acceptance

1. Produce a version inventory from authoritative sources at baseline lock.
2. Select the newest stable compatible release of every applicable system, with explicit evidence.
3. Prove installed package and runtime versions match the immutable lock.
4. Exercise cross-system contract, fixture, integration, and deployed-browser tests.
5. Reject a newer incompatible release rather than silently downgrading.
6. Verify that Administration shows actual versions and investigation pages do not.
7. Preserve the full manifest and verification report alongside the rehearsal evidence.

This specification does not claim any current version is the latest; it requires the next run to establish that fact.
