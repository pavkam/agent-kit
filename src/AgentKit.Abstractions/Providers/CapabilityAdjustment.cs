// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One declared change made to a request because the chosen model could not
/// support what was originally asked for.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Adjustments make downgrade auditable. A validator permitted to adjust a
/// request must emit one of these per change, so no semantic loss ever
/// happens without the caller being told exactly what was dropped.
/// </para>
/// </remarks>
public sealed record CapabilityAdjustment
{
    private readonly string _description;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityAdjustment"/>
    /// record.
    /// </summary>
    /// <param name="capability">The behavior that was adjusted away.</param>
    /// <param name="description">
    /// A redacted, human-readable description of the exact change, such as
    /// disabling streaming or removing parallel tool calls.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capability"/> is not a defined enumeration value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="description"/> is null, empty, or consists only of
    /// whitespace. An undescribed adjustment would be a silent downgrade.
    /// </exception>
    public CapabilityAdjustment(ModelCapabilityKind capability, string description)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Capability = capability;
        _description = description;
    }

    /// <summary>Gets the behavior that was adjusted away.</summary>
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

    /// <summary>Gets the redacted description of the exact change.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Description
    {
        get => _description;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Description));
            _description = value;
        }
    }
}
