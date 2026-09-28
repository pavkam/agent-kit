// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableOperationAddress"/>, the coordinates that locate one operation's durable records.</summary>
/// <remarks>
/// Each typed identity is unwrapped to its underlying <see cref="Guid"/> and rebuilt through the domain constructor, so a
/// persisted address whose agent, session, run, or operation identity was emptied fails on read instead of addressing
/// whatever record happens to share the remaining coordinates. <see cref="TurnId"/> stays nullable because durable work
/// that legitimately occurs between turns has no causal turn; absence is never written as an empty identity.
/// </remarks>
/// <param name="AgentId">The nonempty agent that owns the operation.</param>
/// <param name="SessionId">The nonempty session the operation belongs to.</param>
/// <param name="RunId">The nonempty run retained by the address.</param>
/// <param name="OperationId">The nonempty stable operation identity.</param>
/// <param name="TurnId">The nonempty causal turn, or <see langword="null"/> for work that occurs between turns.</param>
internal sealed record DurableAddressDocument(
    Guid AgentId,
    Guid SessionId,
    Guid RunId,
    Guid OperationId,
    Guid? TurnId)
{
    /// <summary>Projects one domain address into its portable persisted representation.</summary>
    /// <param name="value">The non-null address to project.</param>
    /// <returns>A document carrying every unwrapped coordinate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableAddressDocument FromDomain(DurableOperationAddress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableAddressDocument(
            value.AgentId.Value,
            value.SessionId.Value,
            value.RunId.Value,
            value.OperationId.Value,
            value.TurnId?.Value);
    }

    /// <summary>Reconstructs the exact domain address this document was projected from.</summary>
    /// <returns>An address equal to the projected original.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A required coordinate is empty, or <see cref="TurnId"/> is present but empty.</exception>
    internal DurableOperationAddress ToDomain() => new(
        new AgentId(AgentId),
        new SessionId(SessionId),
        new RunId(RunId),
        new OperationId(OperationId),
        TurnId is { } turnId ? new TurnId(turnId) : null);
}
