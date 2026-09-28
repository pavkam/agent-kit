// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Stores every budget profile registered through <c>AddBudgetProfile</c> or <c>ReplaceBudgetProfile</c>.</summary>
internal sealed class BudgetProfileRegistry
{
    private readonly ConcurrentDictionary<string, BudgetProfileOptions> _profiles = new(StringComparer.Ordinal);

    /// <summary>Gets whether <paramref name="key"/> has been registered.</summary>
    internal bool TryGet(BudgetProfileKey key, [NotNullWhen(true)] out BudgetProfileSnapshot? profile)
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
    internal void Configure(BudgetProfileKey key, Action<BudgetProfileOptions> configure, bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);

        if (replace)
        {
            var options = new BudgetProfileOptions();
            configure(options);
            Validate(options);
            _profiles[key.Value] = options;
            return;
        }

        if (_profiles.TryGetValue(key.Value, out var existing))
        {
            configure(existing);
            Validate(existing);
            return;
        }

        var created = new BudgetProfileOptions();
        configure(created);
        Validate(created);
        _profiles[key.Value] = created;
    }

    private static void Validate(BudgetProfileOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Version.Value, nameof(options.Version));
        ArgumentException.ThrowIfInvalidBudgetScopeLimits([.. options.Limits], nameof(options.Limits));
    }

    private static BudgetProfileSnapshot ToSnapshot(BudgetProfileKey key, BudgetProfileOptions options) =>
        new(
            key,
            options.Version,
            [.. options.Limits],
            [.. options.Policies],
            ComputeFingerprint(key, options));

    private static ContentHash ComputeFingerprint(BudgetProfileKey key, BudgetProfileOptions options)
    {
        var builder = new StringBuilder();
        _ = builder.Append(key.Value).Append('\n');
        _ = builder.Append(options.Version.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var limit in options.Limits.OrderBy(static item => item.Dimension.Value, StringComparer.Ordinal))
        {
            _ = builder.Append(limit.Dimension.Value).Append('|')
                .Append(limit.Unit.Value).Append('|')
                .Append(limit.Value.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(limit.Kind.ToString()).Append('\n');
        }

        foreach (var policy in options.Policies)
        {
            _ = builder.Append(policy.Value).Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return new ContentHash($"sha256:{Convert.ToHexStringLower(hash)}");
    }
}

/// <summary>Contributes one profile configuration during service-provider construction.</summary>
internal interface IBudgetProfileContributor
{
    /// <summary>Applies this contributor's profile configuration.</summary>
    public void Contribute(BudgetProfileRegistry registry);
}

internal sealed class BudgetProfileContributor(BudgetProfileKey key, Action<BudgetProfileOptions> configure, bool replace): IBudgetProfileContributor
{
    internal BudgetProfileKey Key => key;

    public void Contribute(BudgetProfileRegistry registry) => registry.Configure(key, configure, replace);
}

internal sealed class BudgetProfileRegistryInitializer
{
    public BudgetProfileRegistryInitializer(BudgetProfileRegistry registry, IEnumerable<IBudgetProfileContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contributors);
        foreach (var contributor in contributors)
        {
            contributor.Contribute(registry);
        }
    }
}
