// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One registration source's discovered registrations for a requested profile.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="IHookCatalog"/> merges the snapshots from every registered
/// <see cref="IHookRegistrationSource"/> into one <see cref="HookCatalogSnapshot"/>, rejecting duplicate
/// <see cref="HookRegistrationDescriptor.Id"/> values within the same point unless an explicit replacement source
/// named them.
/// </remarks>
public sealed record HookRegistrationSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationSnapshot"/> record.</summary>
    /// <param name="registrations">The registrations this source discovered for the requested profile.</param>
    /// <exception cref="ArgumentException"><paramref name="registrations"/> is default or contains a null element.</exception>
    public HookRegistrationSnapshot(ImmutableArray<HookRegistrationDescriptor> registrations)
    {
        ArgumentException.ThrowIfContainsNull(registrations, nameof(registrations));
        Registrations = registrations;
    }

    /// <summary>Gets the registrations this source discovered for the requested profile.</summary>
    public ImmutableArray<HookRegistrationDescriptor> Registrations { get; }

    /// <inheritdoc/>
    public bool Equals(HookRegistrationSnapshot? other) => other is not null && Registrations.SequenceEqual(other.Registrations);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var registration in Registrations)
        {
            hash.Add(registration);
        }

        return hash.ToHashCode();
    }
}
