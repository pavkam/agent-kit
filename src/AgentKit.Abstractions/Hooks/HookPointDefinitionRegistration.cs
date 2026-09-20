// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Registers one closed hook point's identity and type contract for catalog capture and composition validation.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. It carries the non-generic facts every registration for <see cref="Point"/> must agree
/// with; the closed <see cref="HookPointDefinition{THook,TEventArgs}"/> supplies invocation and validation behavior.
/// <see cref="IHookCatalog"/> rejects registrations whose <see cref="HookRegistrationDescriptor.Point"/> is absent
/// from the captured point-definition set, and composition validation rejects two registrations that bind the same
/// <see cref="Point"/> to incompatible closed types.
/// </remarks>
public sealed record HookPointDefinitionRegistration
{
    /// <summary>Initializes a new instance of the <see cref="HookPointDefinitionRegistration"/> record.</summary>
    /// <param name="point">The stable hook point identity.</param>
    /// <param name="hookInterface">The closed hook interface type for this point.</param>
    /// <param name="eventArgsType">The closed event-argument type for this point.</param>
    /// <param name="kind">This point's mutability classification.</param>
    /// <param name="failureInvariant">The minimum failure mode this point permits.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> is default, <paramref name="kind"/> is not a defined value, or <paramref name="kind"/>
    /// is not <see cref="HookPointKind.Observational"/> while <paramref name="failureInvariant"/> is
    /// <see cref="HookFailureMode.IsolateAndDiagnose"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="hookInterface"/> or <paramref name="eventArgsType"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="hookInterface"/> or <paramref name="eventArgsType"/> is not an interface or does not derive from
    /// <see cref="AgentHookEventArgs"/>.
    /// </exception>
    public HookPointDefinitionRegistration(
        HookPointId point,
        Type hookInterface,
        Type eventArgsType,
        HookPointKind kind,
        HookFailureMode failureInvariant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentNullException.ThrowIfNull(hookInterface);
        ArgumentNullException.ThrowIfNull(eventArgsType);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(failureInvariant);
        if (kind != HookPointKind.Observational && failureInvariant == HookFailureMode.IsolateAndDiagnose)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureInvariant),
                failureInvariant,
                "A mutating or short-circuiting hook point can never declare an isolating failure invariant.");
        }

        if (!hookInterface.IsInterface)
        {
            throw new ArgumentException("Hook interface type must be an interface.", nameof(hookInterface));
        }

        if (!typeof(AgentHookEventArgs).IsAssignableFrom(eventArgsType))
        {
            throw new ArgumentException("Event argument type must derive from AgentHookEventArgs.", nameof(eventArgsType));
        }

        Point = point;
        HookInterface = hookInterface;
        EventArgsType = eventArgsType;
        Kind = kind;
        FailureInvariant = failureInvariant;
    }

    /// <summary>Gets the stable hook point identity.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the closed hook interface type for this point.</summary>
    public Type HookInterface { get; }

    /// <summary>Gets the closed event-argument type for this point.</summary>
    public Type EventArgsType { get; }

    /// <summary>Gets this point's mutability classification.</summary>
    public HookPointKind Kind { get; }

    /// <summary>Gets the minimum failure mode this point permits.</summary>
    public HookFailureMode FailureInvariant { get; }
}
