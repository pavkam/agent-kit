# AgentKit.Memory.Tests

Verifies the `AgentKit.Memory` runtime: profile compilation and runtime
selection, the memory coordinator, the retrieval pipeline and its first-party
sources, policy and event dispatch, the retrieval budget policy, the
deterministic document chunker, registration, and the operation signals.
Collaborators are deterministic fakes and the in-memory storage adapters; no
live model, embedding, or storage service is contacted.
