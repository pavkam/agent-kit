// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides the closed result hierarchy for <see cref="IHookInvocationTracker.TryEnter"/>.</summary>
public abstract record HookInvocationTrackingResult
{
    /// <summary>Initializes a result inside the closed Abstractions hierarchy.</summary>
    private protected HookInvocationTrackingResult() { }
}
