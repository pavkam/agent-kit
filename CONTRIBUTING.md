# Contributing to AgentKit

Help make AgentKit easier to use and safer to extend. Useful contributions
include reproducible bug reports, clearer examples, documentation fixes,
contract tests, and focused implementations.

Participation follows the [Code of Conduct](CODE_OF_CONDUCT.md).

## Get a working checkout

Install the .NET SDK selected by [global.json](global.json) (10.0.203 with
compatible patch roll-forward), Node.js 22 or later, and GNU Make. Fork the
repository if you need your own push destination, then clone your fork or the
main repository:

```sh
git clone https://github.com/pavkam/agent-kit.git
cd agent-kit
make restore
make build
```

The [project catalog](docs/packages/index.md) maps libraries to their tests.
Start with the README of the project you intend to change.

## Choose the right scope

For a bug, include a small reproduction, expected and actual behavior, the
commit or package version, and relevant environment details. Remove credentials
and private content from diagnostics before sharing them.

For a new feature or public contract change, describe the use case and proposed
owner in an issue or draft pull request before building a large implementation.
Small fixes and documentation improvements can go straight to a pull request.

The [architecture](docs/architecture/index.md) and linked concept specifications
define the intended behavior. Read the relevant owner page,
[AGENTS.md](AGENTS.md), and its routed repository skill before changing a
contract. Existing code and tests are evidence of implementation; they do not
override the design.

## Make the change

1. Identify the contract, implementation, and affected callers. Keep contracts
   provider-neutral and dependencies directed inward.
2. For a behavior change, add a focused regression test and confirm it exposes
   the intended problem before implementing the fix.
3. Update the owning specification and acceptance scenarios when the contract
   changes. Update XML documentation, the project README, and any affected
   developer guide alongside the code.
4. Run the focused tests, then the repository quality gates below.
5. Review the diff for unrelated edits and compatibility changes.

Use .NET 10 and C# 14 conventions from the
[C# guide](.agents/skills/references/modern-csharp.md). Public and internal
boundaries need substantive XML documentation and explicit argument validation.
Registration extensions use `IServiceCollection` and state their replacement,
duplicate-registration, and lifetime behavior.

Tests use xUnit v3, Shouldly, and Arrange/Act/Assert. Name new tests
`MethodName_WhenThis_ThatIsExpected`. Keep unit tests deterministic and free of
live service credentials. Put shared fakes in
[AgentKit.Test.Shared](tests/AgentKit.Test.Shared/README.md), and reusable
behavioral suites in
[AgentKit.Conformance](tests/AgentKit.Conformance/README.md).

## Verify your work

Run a focused project from the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.Tests/AgentKit.Providers.Tests.csproj --configuration Release --timeout 300s
```

Before submitting a code change, run the same build, test, and lint gates used
by [CI](.github/workflows/ci.yml):

```sh
make format
make lint
make build
make test
```

For a documentation-only change, format the files you edited, then run:

```sh
npm run format:check
npm run lint:markdown
```

Check relative links and heading anchors, and run any changed examples. Keep
formatting limited to your change if a repository-wide formatter would rewrite
unrelated files. The [testing guide](docs/testing/index.md) explains focused
runs, conformance, coverage, and API snapshots.

The bundled known-model catalog in `AgentKit.Providers` is generated, not
hand-edited. To pick up newly released models or price changes, run
`npm run models:import`, review the diff to
`src/AgentKit.Providers/Resources/known-models.json`, and let
`KnownModelCatalogTests` validate it.

Public API changes require compatibility review. Follow the
[snapshot update instructions](tests/AgentKit.Compatibility.Tests/README.md)
only for an intentional change; describe breaking changes and migration steps in
the pull request.

## Write documentation developers can use

Lead with what a developer can do, give the shortest complete example, and
explain its expected result. Distinguish working behavior from planned
architecture. Never present an illustrative API sketch as a runnable example.

Every solution project needs a `README.md` describing its purpose and linking to
related projects, tests, and reference documentation. Documentation folders use
`index.md` for navigation, with descriptive filenames for individual topics.
Keep badges tied to real workflows or verifiable repository metadata.

## Open a pull request

Explain the concrete problem and resulting behavior. Link the issue or governing
specification, list the verification you ran, and describe any remaining
limitations or compatibility impact. Keep unrelated refactors in separate
changes. The [pull request template](.github/pull_request_template.md) provides
the review checklist.
