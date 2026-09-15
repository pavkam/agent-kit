# AgentKit.Tools.Skill

Activate captured skill content through an explicit security boundary.

Use this tool when a coding host offers selectable skills. The host owns
resource discovery and trust; activating a skill does not widen permissions.

`AddSkillTool` also registers `SkillToolPresentationFormatter` as an additive
`IToolPresentationFormatter`. Applications using `IToolPresenter` receive
readable inventory/activation summaries and literal Markdown skill content from
the exact captured skill descriptor. Presentation omits backing paths, catalog
and content fingerprints, and transport JSON; activated text remains explicitly
non-authoritative data.

## Use this project

Start with `AddSkillTool` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Tools.Resource](../AgentKit.Tools.Resource/README.md) — load
  configured resources through bounded, security-aware operations.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Skill.Tests](../../tests/AgentKit.Tools.Skill.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
