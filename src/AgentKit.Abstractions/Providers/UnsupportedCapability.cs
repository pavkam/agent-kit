// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One behavior the request requires that the chosen model cannot provide.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Reporting the specific <see cref="ModelCapabilityKind"/> rather than a
/// message lets a caller react programmatically — for example by rerunning
/// selection without that requirement — instead of parsing text.
/// </para>
/// </remarks>
public sealed record UnsupportedCapability
{
    private readonly string _reason;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCapability"/>
    /// record.
    /// </summary>
    /// <param name="capability">The behavior that is not supported.</param>
    /// <param name="reason">
    /// A redacted, human-readable explanation. It must not contain prompts,
    /// model output, or credentials.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capability"/> is not a defined enumeration value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public UnsupportedCapability(ModelCapabilityKind capability, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Capability = capability;
        _reason = reason;
    }

    /// <summary>Gets the behavior that is not supported.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public ModelCapabilityKind Capability
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Capability));
            field = value;
        }
    }

    /// <summary>Gets the redacted explanation.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Reason
    {
        get => _reason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Reason));
            _reason = value;
        }
    }
}
