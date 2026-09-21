// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using System.Collections.Frozen;

using Microsoft.Extensions.Options;

/// <summary>Resolves captured policy-snapshot references against the engine's frozen effective-policy publications.</summary>
/// <remarks>
/// The catalog captures configuration once at construction. It never falls back to the latest publication when an exact
/// reference cannot be resolved.
/// </remarks>
public sealed class SecurityPolicyCatalog: ISecurityPolicyCatalog
{
    private readonly FrozenDictionary<SecurityPolicySnapshotReference, SecurityPolicySnapshotReference> _snapshots;

    /// <summary>Captures the configured and published policy-snapshot references for this composition.</summary>
    /// <param name="options">The permission options whose <see cref="AgentPermissionOptions.PolicySnapshot"/> is retained.</param>
    /// <param name="publications">Every immutable security-profile publication registered in the composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="publications"/> is null.</exception>
    /// <exception cref="ArgumentException">Two retained sources publish conflicting content under the same reference.</exception>
    public SecurityPolicyCatalog(
        IOptions<AgentPermissionOptions> options,
        IEnumerable<SecurityProfilePublication> publications)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(publications);
        var optionValues = options.Value;
        ArgumentNullException.ThrowIfNull(optionValues);

        var captured = new Dictionary<SecurityPolicySnapshotReference, SecurityPolicySnapshotReference>();
        Retain(captured, optionValues.PolicySnapshot);
        Retain(captured, SecurityPolicyEvaluationContexts.CreateUncapturedReference(new SecurityPolicyVersion(optionValues.PolicyVersion)));

        foreach (var publication in publications)
        {
            ArgumentNullException.ThrowIfNull(publication, nameof(publications));
            Retain(captured, publication.PolicySnapshot);
        }

        _snapshots = captured.ToFrozenDictionary();
    }

    /// <inheritdoc/>
    public ValueTask<SecurityPolicySnapshotResult> ResolveAsync(
        SecurityPolicySnapshotReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        return _snapshots.ContainsKey(reference)
            ? new ValueTask<SecurityPolicySnapshotResult>(new SecurityPolicySnapshotResolved(reference))
            : new ValueTask<SecurityPolicySnapshotResult>(
                new SecurityPolicySnapshotStale(reference, "The requested policy snapshot is no longer retained."));
    }

    private static void Retain(
        Dictionary<SecurityPolicySnapshotReference, SecurityPolicySnapshotReference> captured,
        SecurityPolicySnapshotReference? reference)
    {
        if (reference is null)
        {
            return;
        }

        if (captured.TryGetValue(reference, out var existing))
        {
            ArgumentException.ThrowIfNotEqual(existing, reference, nameof(reference));
            return;
        }

        captured.Add(reference, reference);
    }
}
