// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates the writable state of one hook point's event arguments after every hook invocation.</summary>
/// <typeparam name="TEventArgs">The closed event-argument type for one hook point.</typeparam>
/// <remarks>
/// A closed <see cref="HookPointDefinition{THook,TEventArgs}"/> supplies exactly one validator. The default,
/// supplied by <c>AgentKit.Hooks</c>, delegates to <see cref="AgentHookEventArgs.Validate"/> so existing derived
/// event-argument types keep their own validation logic; a point may supply a dedicated validator instead when
/// its invariant needs information the event arguments alone do not carry.
/// </remarks>
public interface IHookMutationValidator<in TEventArgs>
    where TEventArgs : AgentHookEventArgs
{
    /// <summary>Validates the current writable state of <paramref name="eventArgs"/>.</summary>
    /// <param name="eventArgs">The event arguments a hook has just finished mutating.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    /// <exception cref="HookValidationException">A writable property has been left in a state this hook point does not permit.</exception>
    public void Validate(TEventArgs eventArgs);
}
