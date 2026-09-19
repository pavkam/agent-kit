// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the boundary at which a hook profile's dynamic configuration change may produce a newly captured hook catalog snapshot.</summary>
/// <remarks>
/// Dynamic hook configuration can never change the sequence already executing within one captured catalog; it can
/// only take effect the next time a catalog is captured at a declared boundary. A profile that has already started
/// dispatching under an earlier capture is unaffected by a reload that happens to complete mid-dispatch.
/// </remarks>
public enum HookReloadBoundary
{
    /// <summary>A configuration change takes effect the next time a run's catalog is captured, at run start.</summary>
    NextRun,

    /// <summary>A configuration change takes effect the next time a turn's catalog is captured, at turn start.</summary>
    NextTurn
}
