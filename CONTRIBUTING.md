# Contributing to AgentKit

AgentKit is an abstractions-first framework. New behavior must remain
replaceable, testable without a live provider, and usable through dependency
injection.

Before changing code:

1. Read [AGENTS.md](AGENTS.md) and the relevant skill under
   [`.agents/skills/`](.agents/skills/).
2. Identify the abstraction, default implementation, integration package, and
   conformance-test impact.
3. Verify volatile provider or protocol behavior against current primary
   documentation.

Before opening a pull request, run:

```bash
make format
make lint
make build
make test
```

Keep pull requests focused. Document public APIs with XML comments, include
tests for observable behavior, and explain compatibility or migration impact
when changing a contract.
