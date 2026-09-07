# Architecture test coverage

This project evaluates every project reference below `src` under the same
configuration that built the test assembly. It checks graph identity and
immutability, rejects missing or duplicate targets and directed cycles, and
enforces the documented inward dependency rules for `AgentKit.Abstractions`, the
`AgentKit` facade, shared `AgentKit.Observability`, and the named behavioral
runtime packages.

The checker deliberately does not validate leaf-to-leaf protocol ownership, the
runtime constructor/factory dependency graph, or XML documentation inside other
projects. Those boundaries require separate architecture decisions and checks;
this suite does not turn current provider edges into exceptions.
