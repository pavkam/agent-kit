// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The mutability classification a closed hook point definition declares for its boundary.</summary>
/// <remarks>
/// This classification, not a caller's requested <see cref="HookFailureMode"/>, decides whether a dispatch may
/// isolate a failing invocation. Only <see cref="Observational"/> points may isolate; <see cref="Mutating"/> and
/// <see cref="ShortCircuiting"/> points always fail the owning operation on any hook exception, invalid mutation,
/// or cancellation, matching the transform- and security-relevant invariant documented in the hooks architecture.
/// </remarks>
public enum HookPointKind
{
    /// <summary>The point is read-only and purely observational; a failing invocation may be isolated and diagnosed.</summary>
    Observational,

    /// <summary>The point exposes writable state that later work depends on; a failing invocation always fails the owning operation.</summary>
    Mutating,

    /// <summary>The point exposes a typed short-circuit outcome; a failing invocation always fails the owning operation.</summary>
    ShortCircuiting
}
