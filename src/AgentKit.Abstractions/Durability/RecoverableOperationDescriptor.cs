// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The complete, immutable declaration of one recoverable operation: what it
/// is, what it may safely do, who owns its retry and deadline, and how it is
/// identified after process loss.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The descriptor is committed before any effect starts. Everything a
/// recovering worker needs in order to decide what to do is either here or in
/// the journal; nothing is inferred from live run state, because by
/// definition that state is gone. It carries no delegate, task, cancellation
/// source, service, or credential, so it can be serialized safely.
/// </para>
/// <para>
/// Identity is composed through <see cref="DurableOperationAddress"/> rather
/// than repeated as loose fields, so that the address used to look an
/// operation up is provably the same value that was recorded with it.
/// </para>
/// </remarks>
public sealed record RecoverableOperationDescriptor
{
    private readonly DurableOperationAddress _address;
    private readonly DurableExecutionContext _executionContext;
    private readonly OperationPayload _input;
    private readonly ExtensionData _extensions;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoverableOperationDescriptor"/> record.
    /// </summary>
    /// <param name="address">The operation's durable coordinates.</param>
    /// <param name="executionContext">
    /// The captured durability composition the operation runs under.
    /// </param>
    /// <param name="name">The deterministic operation name.</param>
    /// <param name="version">
    /// The version of this operation's serialized contract.
    /// </param>
    /// <param name="idempotencyKey">
    /// The key an effect owner can use to collapse duplicate attempts.
    /// </param>
    /// <param name="input">The versioned serialized input.</param>
    /// <param name="retryOwner">The component responsible for retrying.</param>
    /// <param name="timeoutOwner">
    /// The component responsible for enforcing the deadline.
    /// </param>
    /// <param name="cancellation">
    /// What cancelling this operation actually achieves.
    /// </param>
    /// <param name="effect">The material effect the operation performs.</param>
    /// <param name="idempotency">
    /// Whether repeating the operation is safe, and on what basis.
    /// </param>
    /// <param name="deadline">
    /// The instant after which the operation is considered expired by its
    /// declared timeout owner.
    /// </param>
    /// <param name="causalParentId">
    /// The operation that caused this one, when it is scoped child work.
    /// </param>
    /// <param name="extensions">
    /// Additional provider- or host-specific data preserved across recovery.
    /// Defaults to <see cref="ExtensionData.Empty"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/>, <paramref name="executionContext"/>, or
    /// <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="retryOwner"/>, <paramref name="timeoutOwner"/>,
    /// <paramref name="cancellation"/>, <paramref name="effect"/>, or
    /// <paramref name="idempotency"/> is not a defined enumeration value, or
    /// <paramref name="causalParentId"/> is supplied as a default, empty
    /// identity.
    /// </exception>
    public RecoverableOperationDescriptor(
        DurableOperationAddress address,
        DurableExecutionContext executionContext,
        DurableOperationName name,
        DurableOperationVersion version,
        IdempotencyKey idempotencyKey,
        OperationPayload input,
        DurableRetryOwner retryOwner,
        DurableTimeoutOwner timeoutOwner,
        CancellationSemantics cancellation,
        SecurityEffect effect,
        IdempotencyClassification idempotency,
        DateTimeOffset deadline,
        OperationId? causalParentId = null,
        ExtensionData? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfUndefined(retryOwner);
        ArgumentOutOfRangeException.ThrowIfUndefined(timeoutOwner);
        ArgumentOutOfRangeException.ThrowIfUndefined(cancellation);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentOutOfRangeException.ThrowIfUndefined(idempotency);
        ThrowIfDefaultParent(causalParentId, nameof(causalParentId));

        _address = address;
        _executionContext = executionContext;
        Name = name;
        Version = version;
        IdempotencyKey = idempotencyKey;
        _input = input;
        RetryOwner = retryOwner;
        TimeoutOwner = timeoutOwner;
        Cancellation = cancellation;
        Effect = effect;
        Idempotency = idempotency;
        Deadline = deadline;
        CausalParentId = causalParentId;
        _extensions = extensions ?? ExtensionData.Empty;
    }

    /// <summary>Gets the operation's durable coordinates.</summary>
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

    /// <summary>Gets the deterministic operation name.</summary>
    public DurableOperationName Name { get; init; }

    /// <summary>Gets the version of this operation's serialized contract.</summary>
    public DurableOperationVersion Version { get; init; }

    /// <summary>
    /// Gets the key an effect owner can use to collapse duplicate attempts
    /// into one effect.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; init; }

    /// <summary>Gets the versioned serialized input.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public OperationPayload Input
    {
        get => _input;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Input));
            _input = value;
        }
    }

    /// <summary>Gets the component responsible for retrying.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public DurableRetryOwner RetryOwner
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(RetryOwner));
            field = value;
        }
    }

    /// <summary>Gets the component responsible for enforcing the deadline.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public DurableTimeoutOwner TimeoutOwner
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(TimeoutOwner));
            field = value;
        }
    }

    /// <summary>Gets what cancelling this operation actually achieves.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public CancellationSemantics Cancellation
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Cancellation));
            field = value;
        }
    }

    /// <summary>Gets the material effect the operation performs.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public SecurityEffect Effect
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Effect));
            field = value;
        }
    }

    /// <summary>
    /// Gets whether repeating the operation is safe, and on what basis.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public IdempotencyClassification Idempotency
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Idempotency));
            field = value;
        }
    }

    /// <summary>
    /// Gets the instant after which the declared timeout owner considers the
    /// operation expired.
    /// </summary>
    /// <value>
    /// An absolute instant produced from the injected
    /// <see cref="TimeProvider"/>, never from an ambient clock, so recovery
    /// timing is deterministic in tests.
    /// </value>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>
    /// Gets the operation that caused this one, or <see langword="null"/>
    /// when the operation is not scoped child work.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer supplies a default, empty identity. Absent causality is
    /// expressed as <see langword="null"/>.
    /// </exception>
    public OperationId? CausalParentId
    {
        get;
        init
        {
            ThrowIfDefaultParent(value, nameof(CausalParentId));
            field = value;
        }
    }

    /// <summary>
    /// Gets additional provider- or host-specific data preserved across
    /// recovery.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>. Use
    /// <see cref="ExtensionData.Empty"/> to express "no extensions".
    /// </exception>
    public ExtensionData Extensions
    {
        get => _extensions;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Extensions));
            _extensions = value;
        }
    }

    private static void ThrowIfDefaultParent(OperationId? operationId, string paramName)
    {
        if (operationId is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, paramName);
        }
    }
}
