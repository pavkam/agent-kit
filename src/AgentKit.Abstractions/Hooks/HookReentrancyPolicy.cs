// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Whether a hook registration may be re-entered by a nested dispatch of the same hook point on the same call path.</summary>
/// <remarks>
/// AgentKit hooks never use async-local or other ambient state as the authoritative reentrancy mechanism; the
/// active dispatch depth for one point is tracked explicitly by the invocation tracker owned by the current
/// activation lease. A hook does not re-enter its own point merely because it calls an allowed lower-level
/// service; explicit reentrancy must be declared here and stays bounded by the host's
/// <c>AgentHookOptions.MaximumInvocationDepth</c> ceiling even when <see cref="Bounded"/> is declared.
/// </remarks>
public enum HookReentrancyPolicy
{
    /// <summary>A nested dispatch of the same hook point on the same call path is rejected.</summary>
    Forbidden,

    /// <summary>A nested dispatch of the same hook point on the same call path is permitted up to the host's configured depth ceiling.</summary>
    Bounded
}
