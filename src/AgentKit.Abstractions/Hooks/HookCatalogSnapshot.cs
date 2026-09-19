// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One immutable capture of every hook registration for one profile, across every hook point.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="IHookCatalog"/> produces this once per its documented engine, agent, run,
/// turn, or operation scope; a dispatch filters <see cref="Registrations"/> to the registrations for the point
/// being dispatched and resolves their order fresh each time through <see cref="IHookOrderResolver"/>. A new
/// capture is produced only at a declared <see cref="HookReloadBoundary"/>; it never changes the sequence a
/// dispatch already started with.
/// </remarks>
public sealed record HookCatalogSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="HookCatalogSnapshot"/> record.</summary>
    /// <param name="profileKey">The hook profile this capture belongs to.</param>
    /// <param name="version">This capture generation's identity.</param>
    /// <param name="registrations">Every captured registration across every hook point in this profile.</param>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> or <paramref name="version"/> is default (blank), or <paramref name="registrations"/> contains a null element.</exception>
    public HookCatalogSnapshot(
        HookProfileKey profileKey,
        HookCatalogVersion version,
        ImmutableArray<HookRegistrationDescriptor> registrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfContainsNull(registrations, nameof(registrations));

        ProfileKey = profileKey;
        Version = version;
        Registrations = registrations;
    }

    /// <summary>Gets the hook profile this capture belongs to.</summary>
    public HookProfileKey ProfileKey { get; }

    /// <summary>Gets this capture generation's identity.</summary>
    public HookCatalogVersion Version { get; }

    /// <summary>Gets every captured registration across every hook point in this profile.</summary>
    public ImmutableArray<HookRegistrationDescriptor> Registrations { get; }

    /// <inheritdoc/>
    public bool Equals(HookCatalogSnapshot? other) =>
        other is not null
        && ProfileKey.Equals(other.ProfileKey)
        && Version.Equals(other.Version)
        && Registrations.SequenceEqual(other.Registrations);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ProfileKey);
        hash.Add(Version);
        foreach (var registration in Registrations)
        {
            hash.Add(registration);
        }

        return hash.ToHashCode();
    }
}
