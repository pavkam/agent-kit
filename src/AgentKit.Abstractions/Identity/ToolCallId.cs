// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one tool call from the moment the model requests it through
/// its exactly-one terminal result. A <see cref="ToolCallPart"/> and the
/// <see cref="ToolResultPart"/> that answers it always share the same
/// <see cref="ToolCallId"/>, which is how the runtime correlates a request
/// with its outcome instead of relying on array position or provider-only
/// identifiers.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share, compare, and use as a correlation key across threads without
/// synchronization.
/// </para>
/// <para>
/// This is the authoritative AgentKit identity for a call; a provider's own
/// call identifier (see <see cref="ProviderToolCallId"/>) is preserved
/// separately as external correlation and never substituted for this value.
/// A <see cref="ToolCallId"/> is normally minted by the injected
/// <see cref="IIdentifierGenerator{ToolCallId}"/> when the loop first
/// records the call, before authorization or execution begins, so the call
/// is durably traceable even if execution is later denied, deferred, or
/// cancelled.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="ToolCallId"/> always addresses a real call.
/// </para>
/// </remarks>
public readonly record struct ToolCallId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallId"/> struct,
    /// validating that it addresses a real call rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real call.
    /// </exception>
    public ToolCallId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique identifier. This is the only
    /// state the type carries, and it never changes after construction.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the canonical, lowercase, hyphenated text form of this
    /// identity (GUID "D" format), suitable for logging, storage keys, and
    /// round-tripping through configuration or external protocols.
    /// </summary>
    public override string ToString() => Value.ToString("D");
}
