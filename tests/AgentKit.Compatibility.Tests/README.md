# Public API snapshots

Normal test runs compare each evaluated packable source assembly with its
checked-in snapshot and never approve changes.

The snapshots describe the compiled `net10.0` consumer surface: public types
plus public and protected members, including emitted extension members, nullable
metadata, defaults, inheritance, and generic constraints. They intentionally
exclude internal implementation details, XML documentation, method bodies, and
behavioral compatibility; those remain covered by architecture, documentation,
and conformance tests.

PublicApiGenerator 11.5.4 recognizes the non-generic C# 14 extension receivers
used by AgentKit and renders their declared extension block. It also exposes the
compiler-emitted static accessor for an extension property, so snapshots contain
that redundant metadata-level member. It cannot currently render a generic
extension receiver whose member returns its nullable receiver type parameter
(`T?`); its extension-block parser throws rather than producing an incomplete
snapshot. The extractor fixture keeps this limitation explicit, and adding that
public shape requires upgrading or replacing the extractor first.

To approve an intentional API change, run the explicit updater. It generates
received files, copies them to their deterministic verified paths, and reruns
the focused suite:

```sh
./tests/AgentKit.Compatibility.Tests/update-snapshots.sh
```

Review the resulting snapshot diff before committing it. Verify intentionally
provides no ambient auto-approval setting in this project. The updater never
guesses that a package was removed: when exact-set coverage reports stale
`*.verified.txt` files, delete exactly the reported files and rerun the updater.
Received files are excluded from coverage and cleared before every generation
attempt.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Compatibility.Tests/AgentKit.Compatibility.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md) — shared
  consumer contracts.
- [AgentKit](../../src/AgentKit/README.md) — engine composition and lifecycle.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — reusable
  behavioral evidence.
- [Testing guide](../../docs/testing/index.md) — test layers and repository
  commands.
- [Project catalog](../../docs/packages/index.md) — all source and test
  projects.
