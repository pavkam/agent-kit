# AgentKit.Memory

Durable memory lifecycle, document publication, and policy-governed retrieval
for AgentKit.

The package registers the memory coordinator, the document lifecycle
coordinator, the retrieval pipeline, the profile runtime selector, the policy
and event dispatchers, a deterministic text chunker, and two optional
first-party retrieval sources. It installs no store, vector index, embedding
model, reranker, or retrieval source by default: an application selects each
explicitly and a memory profile names exactly what an agent uses.

Retention is fail-closed. A proposed memory is kept only when a registered
memory policy explicitly allows it. Retrieved candidates are untrusted data that
keep their source identity and provenance and pass authorization, stale
filtering, a per-candidate exposure grant, and a budget.

Register with `AddAgentMemory`, add a store leaf such as
`AgentKit.Memory.InMemory`, declare a profile with `AddMemoryProfile`, and
select it on an agent definition. See
[ServiceExtensions.cs](ServiceExtensions.cs).
