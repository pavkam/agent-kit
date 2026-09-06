// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records why one compaction attempt was requested.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="Reason"/> is descriptive only; it is never used as identity,
/// policy, or authority.
/// </remarks>
public sealed record CompactionTrigger
{
    /// <summary>Initializes a new instance of the <see cref="CompactionTrigger"/> record.</summary>
    /// <param name="kind">The observable condition that caused this attempt.</param>
    /// <param name="reason">A human-readable, descriptive explanation.</param>
    /// <param name="causalOperationId">
    /// The operation that caused this trigger, when applicable (for
    /// example, the failed provider request for
    /// <see cref="CompactionTriggerKind.ProviderOverflow"/>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CompactionTrigger(CompactionTriggerKind kind, string reason, OperationId? causalOperationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Kind = kind;
        Reason = reason;
        CausalOperationId = causalOperationId;
    }

    /// <summary>Gets the observable condition that caused this attempt.</summary>
    public CompactionTriggerKind Kind { get; init; }

    /// <summary>Gets a human-readable, descriptive explanation.</summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets the operation that caused this trigger, when applicable.
    /// </summary>
    public OperationId? CausalOperationId { get; init; }
}
