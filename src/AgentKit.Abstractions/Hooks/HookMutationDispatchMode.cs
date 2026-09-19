// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The concurrency model a host permits for invoking mutating hooks within one dispatch.</summary>
/// <remarks>
/// The hooks architecture requires mutating hooks to run sequentially so each one observes the previous hook's
/// validated mutation: <see cref="Sequential"/> is the only mode <c>AgentKit.Hooks</c> currently implements.
/// <see cref="Concurrent"/> is reserved for a future dispatch strategy over hooks that declare no ordering
/// dependency on each other's mutations; selecting it today fails host-options validation rather than silently
/// falling back to sequential dispatch.
/// </remarks>
public enum HookMutationDispatchMode
{
    /// <summary>Mutating hooks run one at a time, in resolved order, each observing every earlier hook's validated mutation.</summary>
    Sequential,

    /// <summary>Reserved for a future concurrency-safe mutation model; not yet implemented by <c>AgentKit.Hooks</c>.</summary>
    Concurrent
}
