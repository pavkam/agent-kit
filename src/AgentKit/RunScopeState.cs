// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Holds the per-run facts the engine discovers only after a run's dependency-injection scope already exists.</summary>
/// <remarks>
/// <para>
/// <see cref="SessionExecutionCapability"/> names the exact session coordinator, run coordinator, and profile a
/// run must use, but the engine only finishes compiling it after resolving those collaborators from the run's own
/// scope (session and security-profile selection, lane discovery, and admission all happen inside the scope). A
/// collaborator resolved from that same scope — such as a session-backed <c>IInputQueue</c> — cannot receive the
/// capability as an ordinary constructor dependency, because nothing exists to construct it from before the scope
/// starts running. This holder is registered scoped so exactly one instance exists per run scope; the engine sets
/// <see cref="Session"/> once it has compiled the capability, and any other scoped registration that needs it
/// resolves it back out through the capability accessor <see cref="ServiceExtensions.AddAgentKit"/> registers.
/// </para>
/// <para>
/// This type is intentionally mutable and is not thread-safe: it is written exactly once, early in one run's
/// scope, by the same logical call that created the scope, before any concurrent work inside that scope reads it.
/// </para>
/// </remarks>
internal sealed class RunScopeState
{
    /// <summary>Gets or sets the compiled session capability for this run's scope.</summary>
    /// <value>
    /// <see langword="null"/> until the engine compiles it for this run; a scoped collaborator resolved before
    /// that point that requires <see cref="SessionExecutionCapability"/> fails to resolve.
    /// </value>
    public SessionExecutionCapability? Session { get; set; }

    /// <summary>Gets or sets this run's complete correlation, for scoped collaborators outside the facade's own assembly.</summary>
    /// <value>
    /// <see langword="null"/> until the engine discovers the run's session address, conversation, and allocated
    /// <see cref="RunId"/>; a scoped collaborator resolved before that point that requires
    /// <see cref="RunScopeIdentity"/> fails to resolve.
    /// </value>
    public RunScopeIdentity? Identity { get; set; }
}
