// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closes one named hook point's hook interface, event-argument type, stable identity, mutability classification, minimum failure behavior, validator, and invocation delegate into a single immutable contract.</summary>
/// <typeparam name="THook">The closed hook interface for this point.</typeparam>
/// <typeparam name="TEventArgs">The closed event-argument type for this point.</typeparam>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization; owning features declare exactly one static instance per point. Runtime code can
/// therefore never pair an arbitrary point name or invocation delegate with an arbitrary object payload: every
/// dispatch through <see cref="IHookDispatcher"/> closes over one of these definitions, and the kernel verifies the
/// definition's <see cref="Id"/> matches both the supplied <see cref="HookDispatchContext"/> and event arguments
/// before resolving or invoking any hook.
/// </remarks>
public sealed record HookPointDefinition<THook, TEventArgs>
    where THook : class
    where TEventArgs : AgentHookEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="HookPointDefinition{THook,TEventArgs}"/> record.</summary>
    /// <param name="id">This point's stable, namespaced identity.</param>
    /// <param name="kind">This point's mutability classification.</param>
    /// <param name="failureInvariant">The minimum failure mode this point permits; monotonic tightening never relaxes below it.</param>
    /// <param name="validator">Validates the event arguments' writable state after every hook invocation.</param>
    /// <param name="invoke">Invokes one resolved hook instance against the shared event arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> is default, <paramref name="kind"/> is not a defined value, or <paramref name="kind"/>
    /// is not <see cref="HookPointKind.Observational"/> while <paramref name="failureInvariant"/> is
    /// <see cref="HookFailureMode.Isolate"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="validator"/> or <paramref name="invoke"/> is null.</exception>
    public HookPointDefinition(
        HookPointId id,
        HookPointKind kind,
        HookFailureMode failureInvariant,
        IHookMutationValidator<TEventArgs> validator,
        HookInvoker<THook, TEventArgs> invoke)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(failureInvariant);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(invoke);
        if (kind != HookPointKind.Observational && failureInvariant == HookFailureMode.Isolate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureInvariant),
                failureInvariant,
                "A mutating or short-circuiting hook point can never declare an isolating failure invariant.");
        }

        Id = id;
        Kind = kind;
        FailureInvariant = failureInvariant;
        Validator = validator;
        Invoke = invoke;
    }

    /// <summary>Gets this point's stable, namespaced identity.</summary>
    public HookPointId Id { get; }

    /// <summary>Gets this point's mutability classification.</summary>
    public HookPointKind Kind { get; }

    /// <summary>Gets the minimum failure mode this point permits.</summary>
    public HookFailureMode FailureInvariant { get; }

    /// <summary>Gets the validator run after every hook invocation.</summary>
    public IHookMutationValidator<TEventArgs> Validator { get; }

    /// <summary>Gets the delegate that invokes one resolved hook instance.</summary>
    public HookInvoker<THook, TEventArgs> Invoke { get; }
}
