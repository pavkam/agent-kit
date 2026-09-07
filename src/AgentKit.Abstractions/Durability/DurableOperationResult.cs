// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The authoritative terminal record of one recoverable operation, including
/// the truthful side-effect certainty that recovery depends on.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// A terminal record is written once. Recording it does not publish the
/// result: a complete outcome may be staged while an earlier parallel sibling
/// still blocks source-ordered publication, which is exactly the
/// <see cref="DurableOperationState.OutcomeReady"/> case that must never
/// reinvoke the effect.
/// </para>
/// <para>
/// A failed result still carries certainty. "The call threw" does not mean
/// the effect did not happen, so a failure whose certainty is
/// <see cref="SideEffectCertainty.Unknown"/> is a reconciliation case rather
/// than a clean failure.
/// </para>
/// </remarks>
public sealed record DurableOperationResult
{
    private readonly DurableOperationAddress _address;
    private readonly DurableExecutionContext _executionContext;
    private readonly OperationPayload _output;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableOperationResult"/> record.
    /// </summary>
    /// <param name="address">The operation this result belongs to.</param>
    /// <param name="executionContext">
    /// The captured durability composition the operation ran under.
    /// </param>
    /// <param name="state">
    /// The terminal lifecycle position, normally
    /// <see cref="DurableOperationState.Completed"/> or
    /// <see cref="DurableOperationState.Faulted"/>.
    /// </param>
    /// <param name="sideEffectCertainty">
    /// What is actually known about whether the external effect occurred.
    /// </param>
    /// <param name="output">The versioned serialized result payload.</param>
    /// <param name="fencingToken">
    /// The ownership generation of the lease committing this record.
    /// </param>
    /// <param name="completedAt">
    /// The instant the terminal record was produced, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="safeFailureMessage">
    /// A redacted, human-readable description of why the operation faulted,
    /// or <see langword="null"/> when it succeeded. It must not contain
    /// prompts, model output, tool arguments, credentials, or raw paths,
    /// because terminal records are durable and widely read.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/>, <paramref name="executionContext"/>, or
    /// <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> or <paramref name="sideEffectCertainty"/> is
    /// not a defined enumeration value, or <paramref name="fencingToken"/> is
    /// the default, unallocated token.
    /// </exception>
    public DurableOperationResult(
        DurableOperationAddress address,
        DurableExecutionContext executionContext,
        DurableOperationState state,
        SideEffectCertainty sideEffectCertainty,
        OperationPayload output,
        FencingToken fencingToken,
        DateTimeOffset completedAt,
        string? safeFailureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));

        _address = address;
        _executionContext = executionContext;
        State = state;
        SideEffectCertainty = sideEffectCertainty;
        _output = output;
        FencingToken = fencingToken;
        CompletedAt = completedAt;
        SafeFailureMessage = safeFailureMessage;
    }

    /// <summary>Gets the operation this result belongs to.</summary>
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

    /// <summary>Gets the terminal lifecycle position.</summary>
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

    /// <summary>Gets the versioned serialized result payload.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public OperationPayload Output
    {
        get => _output;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Output));
            _output = value;
        }
    }

    /// <summary>
    /// Gets the ownership generation of the lease committing this record.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, unallocated token.
    /// </exception>
    public FencingToken FencingToken
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(FencingToken));
            field = value;
        }
    }

    /// <summary>Gets the instant the terminal record was produced.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets a redacted description of why the operation faulted, or
    /// <see langword="null"/> when it succeeded.
    /// </summary>
    /// <value>
    /// Safe diagnostic text only. Durable terminal records outlive the run
    /// and are read by operators and recovery tooling, so protected content
    /// never belongs here.
    /// </value>
    public string? SafeFailureMessage { get; init; }
}
