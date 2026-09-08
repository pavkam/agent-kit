# AgentKit.Permissions.InMemory

Provides deterministic, process-local ISecurityGrantStore storage for tests,
examples, and short-lived applications. It retains grant evidence, remaining
uses, revocation state, and enforcement-intent receipts only for the current
process lifetime.

Register it explicitly with AddInMemorySecurityGrantStore after composing the
security runtime. Production hosts that require recovery use an explicit durable
security-grant storage adapter. Repeating this registration is idempotent;
combining it with another store remains visibly ambiguous until the host removes
the unwanted interface registration explicitly.

Target: **.NET 10**.
