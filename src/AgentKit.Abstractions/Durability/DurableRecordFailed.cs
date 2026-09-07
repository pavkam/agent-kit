// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal could not commit the write for a reason other than fencing,
/// such as a storage outage.
/// </summary>
/// <remarks>
/// <para>
/// This outcome is explicitly ambiguous about durability. A store that cannot
/// prove whether the record committed must report
/// <see cref="Committed"/> as <see langword="null"/> rather than guessing,
/// because a caller that assumes "failed" may duplicate an effect while a
/// caller that assumes "succeeded" may lose one.
/// </para>
/// <para>
/// Failing to record is not the same as failing the operation. When the
/// effect already happened, a lost journal write leaves an unknown-outcome
/// case that recovery must reconcile.
/// </para>
/// </remarks>
public sealed record DurableRecordFailed: DurableRecordResult
{
    private readonly string _safeMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableRecordFailed"/>
    /// record.
    /// </summary>
    /// <param name="safeMessage">
    /// A redacted, human-readable description of the storage failure. It must
    /// not contain credentials, connection strings, or protected content.
    /// </param>
    /// <param name="committed">
    /// <see langword="true"/> when the store proved the record committed
    /// despite the error, <see langword="false"/> when it proved it did not,
    /// and <see langword="null"/> when durability is genuinely unknown.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableRecordFailed(string safeMessage, bool? committed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        _safeMessage = safeMessage;
        Committed = committed;
    }

    /// <summary>Gets the redacted description of the storage failure.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeMessage
    {
        get => _safeMessage;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeMessage));
            _safeMessage = value;
        }
    }

    /// <summary>
    /// Gets whether the record actually committed, or <see langword="null"/>
    /// when that is unknown.
    /// </summary>
    /// <value>
    /// <see langword="null"/> is a truthful and expected value. It is not a
    /// placeholder for "probably false".
    /// </value>
    public bool? Committed { get; init; }
}
