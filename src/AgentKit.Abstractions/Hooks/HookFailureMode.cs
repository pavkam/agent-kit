// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>How a dispatch treats an exception thrown by one hook.</summary>
/// <remarks>
/// The owning operation, not the hook itself, decides this per dispatch
/// call: a hook point that transforms data the operation depends on, or
/// that participates in a security-relevant decision, dispatches with
/// <see cref="FailOperation"/>; a purely observational notification point
/// dispatches with <see cref="Isolate"/> so one misbehaving observer cannot
/// fail an unrelated operation.
/// </remarks>
public enum HookFailureMode
{
    /// <summary>An exception from any hook fails the entire dispatch and propagates to the caller.</summary>
    FailOperation,

    /// <summary>An exception from a hook is swallowed and dispatch continues with the remaining hooks.</summary>
    Isolate
}
