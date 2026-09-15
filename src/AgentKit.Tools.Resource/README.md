# AgentKit.Tools.Resource

Load configured resources through bounded, security-aware operations.

Use this tool to expose resource content under a captured configuration.
Resource descriptions do not grant authority to retrieve arbitrary content.

`AddResourceTool` also registers `ResourceToolPresentationFormatter` as an
additive `IToolPresentationFormatter`. Applications using `IToolPresenter`
receive readable list/read call summaries and literal loaded content from the
exact captured resource descriptor. Presentation omits backing paths, catalog
and content fingerprints, and transport JSON; loaded text remains explicitly
non-authoritative data.

## Use this project

Start with `AddResourceTool` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Tools.Skill](../AgentKit.Tools.Skill/README.md) — activate captured
  skill content through an explicit security boundary.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Resource.Tests](../../tests/AgentKit.Tools.Resource.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
