// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Diagnostics.CodeAnalysis;

/// <summary>A profile catalog that publishes exactly one profile enabling the supplied operation names.</summary>
/// <remarks>
/// Component keys inside the snapshot are fixed placeholders: the boundary tests that use this fake never resolve a
/// journal, lease manager, or backend, because <see cref="RecordingBoundaryCoordinator"/> stands in for the runtime
/// that would.
/// </remarks>
public sealed class FixedDurabilityProfileCatalog: IDurabilityProfileCatalog
{
    private readonly DurabilityProfileSnapshot _profile;

    /// <summary>Initializes a catalog publishing one profile.</summary>
    /// <param name="key">The nondefault profile key the catalog resolves.</param>
    /// <param name="enabledOperations">The operation names the profile enables; an empty list enables none.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
    public FixedDurabilityProfileCatalog(DurabilityProfileKey key, params DurableOperationName[] enabledOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(enabledOperations);
        _profile = new DurabilityProfileSnapshot(
            key,
            new DurabilityProfileVersion(1),
            new DurableBackendKey("test.backend"),
            new DurableJournalKey("test.journal"),
            new DurableLeaseManagerKey("test.leases"),
            new RecoveryPolicyKey("test.policy"),
            [.. enabledOperations],
            new ContentHash("sha256:test-profile"));
    }

    /// <inheritdoc/>
    public bool TryGet(DurabilityProfileKey key, [NotNullWhen(true)] out DurabilityProfileSnapshot? profile)
    {
        profile = key == _profile.Key ? _profile : null;
        return profile is not null;
    }
}
