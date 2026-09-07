// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The payload could not be interpreted by the running codec, normally
/// because it was written under a schema or operation version this build does
/// not understand.
/// </summary>
/// <typeparam name="TState">The state type that was expected.</typeparam>
/// <remarks>
/// This is a first-class outcome rather than an exception because it is an
/// expected consequence of deploying new code over existing durable state.
/// Returning it lets recovery choose migration, a pinned old version, or an
/// explicit stop, instead of a partially-decoded value that silently
/// reinterprets recorded work.
/// </remarks>
public sealed record DurableDecodeIncompatible<TState>: DurableDecodeResult<TState>
{
    private readonly string _safeReason;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableDecodeIncompatible{TState}"/> record.
    /// </summary>
    /// <param name="recordedVersion">
    /// The schema version the payload was written under.
    /// </param>
    /// <param name="safeReason">
    /// A redacted, human-readable explanation of the incompatibility. It must
    /// not contain the payload contents.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeReason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableDecodeIncompatible(SchemaVersion recordedVersion, string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        RecordedVersion = recordedVersion;
        _safeReason = safeReason;
    }

    /// <summary>Gets the schema version the payload was written under.</summary>
    public SchemaVersion RecordedVersion { get; init; }

    /// <summary>Gets the redacted explanation of the incompatibility.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeReason
    {
        get => _safeReason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeReason));
            _safeReason = value;
        }
    }
}
