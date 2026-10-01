# AgentKit.Context.Retrieval

Retrieval-backed context contribution for AgentKit context assembly.

`RetrievalContextContributor` turns the latest user message into an authorized
retrieval query against the agent's selected memory profile, then publishes each
surviving candidate as `RetrievedData` reference data that carries its source
identity and provenance. Retrieved content is data, never instruction, and its
trust never rises above `ContextTrust.RetrievedData`. A refused or unavailable
retrieval contributes nothing and a content-free diagnostic.

Register with `AddRetrievalContextContributor` after `AddAgentContext` and
`AddAgentMemory` (from `AgentKit.Memory`). See
[ServiceExtensions.cs](ServiceExtensions.cs).
