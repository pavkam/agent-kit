// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that one recoverable operation's acceptance record committed before any effect ran.</summary>
/// <remarks>
/// This event is published after <see cref="IDurableOperationJournal.RecordStartAsync"/> returns
/// <see cref="DurableRecorded"/>, so it always describes durable truth rather than an intent. Sinks observe it and
/// cannot alter the operation; publication failure never retracts the committed record.
/// </remarks>
public sealed record DurableOperationAccepted: DurableExecutionEvent
{
    /// <summary>Initializes an acceptance observation.</summary>
    /// <param name="binding">The non-null address and captured execution context of the accepted operation.</param>
    /// <param name="occurredAt">The injected-clock instant at which acceptance committed.</param>
    /// <param name="name">The operation name whose acceptance committed.</param>
    /// <param name="version">The operation version whose acceptance committed.</param>
    /// <param name="fencingToken">The ownership generation that committed the record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="version"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fencingToken"/> is the default value.</exception>
    public DurableOperationAccepted(
        DurableOperationBinding binding,
        DateTimeOffset occurredAt,
        DurableOperationName name,
        DurableOperationVersion version,
        FencingToken fencingToken)
        : base(binding, occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name.Value, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        Name = name;
        Version = version;
        FencingToken = fencingToken;
    }

    /// <summary>Gets the accepted operation name.</summary>
    /// <value>The nonblank stable operation name recorded on the descriptor.</value>
    public DurableOperationName Name { get; }

    /// <summary>Gets the accepted operation version.</summary>
    /// <value>The nonblank version whose codec must decode any replayed payload.</value>
    public DurableOperationVersion Version { get; }

    /// <summary>Gets the ownership generation that committed acceptance.</summary>
    /// <value>The non-default fencing token presented by the accepting worker.</value>
    public FencingToken FencingToken { get; }
}
