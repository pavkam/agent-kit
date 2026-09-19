// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides the closed result hierarchy for <see cref="IHookOrderResolver.Resolve"/>.</summary>
public abstract record HookOrderResult
{
    /// <summary>Initializes a result inside the closed Abstractions hierarchy.</summary>
    private protected HookOrderResult() { }
}
