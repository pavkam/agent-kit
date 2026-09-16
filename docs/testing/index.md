# Testing AgentKit

Start with the tests for the package you changed. Broader suites check shared
contracts, project boundaries, and the compiled public API. Unit tests use
deterministic fakes or local fixtures; they do not require live model accounts.

## Run the tests

From the repository root, run a focused project:

```sh
dotnet test --project tests/AgentKit.Providers.Tests/AgentKit.Providers.Tests.csproj --configuration Release --timeout 300s
```

The repository uses xUnit v3 with Microsoft Testing Platform, selected in
[global.json](../../global.json). Use its `--project` and `--solution` forms.
After a Release build, run the full solution without rebuilding:

```sh
dotnet test --solution AgentKit.slnx --configuration Release --no-build --timeout 300s
```

`make test` restores and builds before running that solution command. For
coverage, `make coverage` creates a report at
`artifacts/coverage/report/index.html`; `make coverage-check` also applies the
configured line-coverage threshold (`COVERAGE_MINIMUM_LINE`, currently 90%).
Coverage measures exercised code, not complete architectural conformance.

[CI](../../.github/workflows/ci.yml) runs `make coverage-check` on every pull
request and push to `main`, then uploads the merged Cobertura report to
[Codecov](https://codecov.io/gh/pavkam/agent-kit), which backs the coverage
badge in [the README](../../README.md).

## Know what each layer proves

| Layer                   | Purpose                                                                        | Where                                                                              |
| ----------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------- |
| Focused component tests | Observable behavior, validation, cancellation, diagnostics, and DI replacement | Each source project's linked test README                                           |
| Shared test support     | Reusable identity/security fixtures and activity observation                   | [AgentKit.Test.Shared](../../tests/AgentKit.Test.Shared/README.md)                 |
| Contract conformance    | Run the same behavioral suite against interchangeable implementations          | [AgentKit.Conformance](../../tests/AgentKit.Conformance/README.md)                 |
| Architecture checks     | Evaluate source project references and enforce the tested dependency rules     | [AgentKit.Architecture.Tests](../../tests/AgentKit.Architecture.Tests/README.md)   |
| API compatibility       | Compare the compiled consumer surface with reviewed snapshots                  | [AgentKit.Compatibility.Tests](../../tests/AgentKit.Compatibility.Tests/README.md) |

Provider tests named `EndToEndTests` exercise adapter pipelines with controlled
fixtures; the name does not imply a live vendor service test. Likewise, an
in-memory store suite does not prove recovery from process loss.

## Add useful evidence

Test the behavior visible to a caller. Use xUnit v3, Shouldly, and
`MethodName_WhenThis_ThatIsExpected` names for new cases. For a swappable
contract, add reusable conformance cases and run them against each applicable
implementation. Keep reusable support in the shared test projects.

For a changed argument constraint, assert the exact exception type and
`ParamName`, boundary values, and validation before effects. For asynchronous or
protected operations, cover cancellation, terminal outcomes, bounded
diagnostics, and denial before an effect. Keep private-method assertions covered
through observable behavior.

Update public API snapshots only after reviewing an intentional API change. The
[compatibility README](../../tests/AgentKit.Compatibility.Tests/README.md)
documents the explicit updater and its limitations.

See [Contributing](../../CONTRIBUTING.md) for the complete workflow and
[testing architecture](../architecture/testing-and-evaluation.md) for normative
requirements.
