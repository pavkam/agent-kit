# AgentKit documentation

Start with a working agent, then follow the parts your application needs.
AgentKit is in alpha; the guides describe current entry points, while the
architecture and concept specifications define the complete intended design.

## Build with AgentKit

| You want to…                                   | Start here                                          |
| ---------------------------------------------- | --------------------------------------------------- |
| Set up a checkout and run your first agent     | [Getting started](getting-started.md)               |
| Build an agent in a few lines                  | [AgentKit.Simple](../src/AgentKit.Simple/README.md) |
| Understand engines, agents, sessions, and runs | [Composing an application](guides/composition.md)   |
| Find a package and its related projects        | [Project catalog](packages/index.md)                |
| Select a provider and check its wire behavior  | [Provider reference](providers/index.md)            |
| Check what still needs implementation or proof | [Current status](getting-started.md#current-status) |
| Run focused tests or add a regression          | [Testing guide](testing/index.md)                   |

## Go deeper

- [Architecture](architecture/index.md) explains package ownership, dependency
  direction, composition, and runtime boundaries.
- [Concept specifications](concepts/index.md) define observable behavior and
  acceptance scenarios for each extension point.
- [Application profiles](profiles/index.md) show how those components fit
  particular products, including a
  [coding harness](profiles/coding-harness/index.md).
- [Provider API contracts](providers/index.md) record protocol and capability
  details for adapter authors.

The architecture index explains
[document authority](architecture/index.md#authority-and-change-rules). Examples
and implementation notes do not override normative requirements.

## Contribute

Read [Contributing](../CONTRIBUTING.md) and the
[Code of Conduct](../CODE_OF_CONDUCT.md). Each source and test project has a
README linking its purpose, collaborators, and relevant evidence. Documentation
folders use `index.md` as their navigation page.
