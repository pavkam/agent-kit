// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides the closed result hierarchy for <see cref="IHookActivationLease.ResolveAsync{THook}"/>.</summary>
/// <typeparam name="THook">The closed hook interface requested.</typeparam>
public abstract record HookInstanceResolution<THook>
    where THook : class
{
    /// <summary>Initializes a result inside the closed Abstractions hierarchy.</summary>
    private protected HookInstanceResolution() { }
}
