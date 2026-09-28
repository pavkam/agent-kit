// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An immutable named budget profile resolved from composition-time registration.</summary>
/// <remarks>
/// Profiles expose limits and ordered policy keys only; they do not reserve capacity or resolve services at runtime.
/// </remarks>
public sealed record BudgetProfileSnapshot
{
    /// <summary>Initializes an immutable profile snapshot.</summary>
    /// <param name="key">The nondefault profile key.</param>
    /// <param name="version">The positive profile revision.</param>
    /// <param name="limits">The limits configured on this profile, which may be empty.</param>
    /// <param name="policies">The ordered policy keys evaluated for this profile.</param>
    /// <param name="configurationFingerprint">The fingerprint of the captured profile configuration.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank, or a limit collection is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configurationFingerprint"/> is default.</exception>
    public BudgetProfileSnapshot(
        BudgetProfileKey key,
        BudgetProfileVersion version,
        ImmutableArray<BudgetLimit> limits,
        ImmutableArray<BudgetPolicyKey> policies,
        ContentHash configurationFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits, nameof(limits));
        ArgumentException.ThrowIfDefault(policies);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFingerprint.Value, nameof(configurationFingerprint));

        Key = key;
        Version = version;
        Limits = limits;
        Policies = policies;
        ConfigurationFingerprint = configurationFingerprint;
    }

    /// <summary>Gets the profile key.</summary>
    public BudgetProfileKey Key { get; }

    /// <summary>Gets the positive profile revision.</summary>
    public BudgetProfileVersion Version { get; }

    /// <summary>Gets the limits configured on this profile.</summary>
    public ImmutableArray<BudgetLimit> Limits { get; }

    /// <summary>Gets the ordered policy keys for this profile.</summary>
    public ImmutableArray<BudgetPolicyKey> Policies { get; }

    /// <summary>Gets the fingerprint of the captured profile configuration.</summary>
    public ContentHash ConfigurationFingerprint { get; }
}
