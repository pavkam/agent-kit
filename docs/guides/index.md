# Developer guides

Each guide takes one thing you will want an agent to do and shows the one or two
lines that do it, what those lines guarantee, and what to change when you
outgrow them.

| Guide                                       | You will learn                                                                                            |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| [Getting started](../getting-started.md)    | Run the smallest complete agent and what each line of it composes.                                        |
| [Storing conversations](storage.md)         | In-memory versus SQLite sessions, and how to list and resume a conversation after a restart.              |
| [Working with files](file-system.md)        | Give the agent a sandboxed directory and the read, search, and edit tools; what the sandbox enforces.     |
| [Permissions and approvals](permissions.md) | How a decision is made, read-only and scoped policies, asking a human before acting, identity, and audit. |
| [Composing an application](composition.md)  | The engine's lifetimes and required collaborators, and the written-out composition behind the sugar.      |

The [use cases](../use-cases/index.md) combine these features into complete
compositions for particular applications, and document the two example programs.
Use the [project catalog](../packages/index.md) to choose a package, or return
to the [documentation home](../index.md) for architecture, concepts, and the
provider reference.
