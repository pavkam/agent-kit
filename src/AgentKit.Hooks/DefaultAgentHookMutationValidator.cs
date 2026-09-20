// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Default mutation validator that delegates to <see cref="AgentHookEventArgs.Validate"/>.</summary>
/// <typeparam name="TEventArgs">The closed event-argument type for one hook point.</typeparam>
internal sealed class DefaultAgentHookMutationValidator<TEventArgs>: IHookMutationValidator<TEventArgs>
    where TEventArgs : AgentHookEventArgs
{
    /// <inheritdoc/>
    public void Validate(TEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        eventArgs.Validate();
    }
}
