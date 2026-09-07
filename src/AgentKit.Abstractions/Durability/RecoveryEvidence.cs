// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Everything durably known about one recoverable operation after process
/// loss, assembled by the journal so that a recovery policy can classify it
/// without guessing.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Evidence describes what was recorded, not what probably happened.
/// <see cref="StartDefinitelyAbsent"/> and
/// <see cref="TerminalResultRecorded"/> are separate facts from
/// <see cref="SideEffectCertainty"/> precisely because the interesting case —
/// dispatched, no terminal record — is the one where the first two are false
/// and certainty is <see cref="SideEffectCertainty.Unknown"/>.
/// </para>
/// </remarks>
public sealed record RecoveryEvidence
{
    private readonly DurableOperationAddress _address;
    private readonly DurableExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryEvidence"/>
    /// record.
    /// </summary>
    /// <param name="address">The operation the evidence describes.</param>
    /// <param name="executionContext">
    /// The captured durability composition the operation started under.
    /// </param>
    /// <param name="state">The persisted lifecycle position.</param>
    /// <param name="sideEffectCertainty">
    /// What is actually known about whether the external effect occurred.
    /// </param>
    /// <param name="startDefinitelyAbsent">
    /// <see langword="true"/> only when records prove the operation was never
    /// dispatched. When in doubt this is <see langword="false"/>.
    /// </param>
    /// <param name="terminalResultRecorded">
    /// <see langword="true"/> when a complete terminal result exists and may
    /// be committed without reinvocation.
    /// </param>
    /// <param name="latestCheckpoint">
    /// The most recent complete state snapshot, when one was recorded.
    /// </param>
    /// <param name="externalReference">
    /// The external owner's handle, when work was handed off.
    /// </param>
    /// <param name="externalIdempotencyKey">
    /// The key accepted by the effect owner, when one was established.
    /// </param>
    /// <param name="lastWriterToken">
    /// The ownership generation of the last successful durable write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> or <paramref name="executionContext"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> or <paramref name="sideEffectCertainty"/> is
    /// not a defined enumeration value, or <paramref name="lastWriterToken"/>
    /// is supplied as a default, unallocated token.
    /// </exception>
    public RecoveryEvidence(
        DurableOperationAddress address,
        DurableExecutionContext executionContext,
        DurableOperationState state,
        SideEffectCertainty sideEffectCertainty,
        bool startDefinitelyAbsent,
        bool terminalResultRecorded,
        DurableCheckpoint? latestCheckpoint = null,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null,
        FencingToken? lastWriterToken = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ThrowIfDefaultToken(lastWriterToken, nameof(lastWriterToken));

        _address = address;
        _executionContext = executionContext;
        State = state;
        SideEffectCertainty = sideEffectCertainty;
        StartDefinitelyAbsent = startDefinitelyAbsent;
        TerminalResultRecorded = terminalResultRecorded;
        LatestCheckpoint = latestCheckpoint;
        ExternalReference = externalReference;
        ExternalIdempotencyKey = externalIdempotencyKey;
        LastWriterToken = lastWriterToken;
    }

    /// <summary>Gets the operation the evidence describes.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableOperationAddress Address
    {
        get => _address;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Address));
            _address = value;
        }
    }

    /// <summary>Gets the captured durability composition.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableExecutionContext ExecutionContext
    {
        get => _executionContext;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ExecutionContext));
            _executionContext = value;
        }
    }

    /// <summary>Gets the persisted lifecycle position.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public DurableOperationState State
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(State));
            field = value;
        }
    }

    /// <summary>
    /// Gets what is actually known about whether the external effect
    /// occurred.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public SideEffectCertainty SideEffectCertainty
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(SideEffectCertainty));
            field = value;
        }
    }

    /// <summary>
    /// Gets whether records prove the operation was never dispatched.
    /// </summary>
    /// <value>
    /// <see langword="true"/> only under proof. Absence of a start record is
    /// not by itself proof when the journal write could have been lost.
    /// </value>
    public bool StartDefinitelyAbsent { get; init; }

    /// <summary>
    /// Gets whether a complete terminal result exists and may be committed
    /// without reinvoking the effect.
    /// </summary>
    public bool TerminalResultRecorded { get; init; }

    /// <summary>
    /// Gets the most recent complete state snapshot, or
    /// <see langword="null"/> when none was recorded.
    /// </summary>
    public DurableCheckpoint? LatestCheckpoint { get; init; }

    /// <summary>
    /// Gets the external owner's handle, or <see langword="null"/> when no
    /// handoff was recorded.
    /// </summary>
    public ExternalOperationReference? ExternalReference { get; init; }

    /// <summary>
    /// Gets the key accepted by the effect owner, or <see langword="null"/>
    /// when no external idempotency was established.
    /// </summary>
    public IdempotencyKey? ExternalIdempotencyKey { get; init; }

    /// <summary>
    /// Gets the ownership generation of the last successful durable write, or
    /// <see langword="null"/> when nothing was written.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer supplies a default, unallocated token. Absence is
    /// expressed as <see langword="null"/>.
    /// </exception>
    public FencingToken? LastWriterToken
    {
        get;
        init
        {
            ThrowIfDefaultToken(value, nameof(LastWriterToken));
            field = value;
        }
    }

    private static void ThrowIfDefaultToken(FencingToken? token, string paramName)
    {
        if (token is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, paramName);
        }
    }
}
