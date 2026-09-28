// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Stores every durability profile registered through <c>AddDurabilityProfile</c> or <c>ReplaceDurabilityProfile</c>.</summary>
/// <remarks>
/// The registry is a composition-time accumulator, not a persistence adapter. It is populated once from ordered
/// contributors while the service provider is built and is then read concurrently by
/// <see cref="InMemoryDurabilityProfileCatalog"/>. Mutation after the provider is built is not part of its contract.
/// </remarks>
internal sealed class DurabilityProfileRegistry
{
    private readonly ConcurrentDictionary<string, DurabilityProfileOptions> _profiles = new(StringComparer.Ordinal);

    /// <summary>Attempts to project one registered profile into an immutable snapshot.</summary>
    /// <param name="key">The profile key to resolve.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the projected snapshot.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> is registered; otherwise <see langword="false"/>.</returns>
    internal bool TryGet(DurabilityProfileKey key, [NotNullWhen(true)] out DurabilityProfileSnapshot? profile)
    {
        profile = null;
        if (key.Value is not { Length: > 0 } value || !_profiles.TryGetValue(value, out var options))
        {
            return false;
        }

        profile = ToSnapshot(key, options);
        return true;
    }

    /// <summary>Registers or updates one named profile.</summary>
    /// <param name="key">The nondefault, non-blank profile key being configured.</param>
    /// <param name="configure">The configuration callback applied to the profile options.</param>
    /// <param name="replace">
    /// When <see langword="true"/>, discards any previously accumulated configuration for <paramref name="key"/> and
    /// applies <paramref name="configure"/> to fresh options; otherwise the callback refines existing options.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured profile leaves a required key or version unset.</exception>
    internal void Configure(DurabilityProfileKey key, Action<DurabilityProfileOptions> configure, bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);

        if (!replace && _profiles.TryGetValue(key.Value, out var existing))
        {
            configure(existing);
            Validate(existing);
            return;
        }

        var options = new DurabilityProfileOptions();
        configure(options);
        Validate(options);
        _profiles[key.Value] = options;
    }

    private static void Validate(DurabilityProfileOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Version.Value, nameof(options.Version));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BackendKey.Value, nameof(options.BackendKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.JournalKey.Value, nameof(options.JournalKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.LeaseManagerKey.Value, nameof(options.LeaseManagerKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RecoveryPolicyKey.Value, nameof(options.RecoveryPolicyKey));
        foreach (var operation in options.EnabledOperations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation.Value, nameof(options.EnabledOperations));
        }
    }

    private static DurabilityProfileSnapshot ToSnapshot(DurabilityProfileKey key, DurabilityProfileOptions options) =>
        new(
            key,
            options.Version,
            options.BackendKey,
            options.JournalKey,
            options.LeaseManagerKey,
            options.RecoveryPolicyKey,
            [.. options.EnabledOperations],
            ComputeFingerprint(key, options));

    private static ContentHash ComputeFingerprint(DurabilityProfileKey key, DurabilityProfileOptions options)
    {
        var builder = new StringBuilder();
        _ = builder.Append(key.Value).Append('\n')
            .Append(options.Version.Value.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(options.BackendKey.Value).Append('\n')
            .Append(options.JournalKey.Value).Append('\n')
            .Append(options.LeaseManagerKey.Value).Append('\n')
            .Append(options.RecoveryPolicyKey.Value).Append('\n');
        foreach (var operation in options.EnabledOperations.Select(static name => name.Value).Order(StringComparer.Ordinal))
        {
            _ = builder.Append(operation).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return new ContentHash($"sha256:{Convert.ToHexStringLower(hash)}");
    }
}
