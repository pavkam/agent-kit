// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Holds validated goal-profile configuration and publishes immutable snapshots of it.</summary>
/// <remarks>
/// Each configuration is validated against the host ceilings when it is applied, so a profile that widens a host limit
/// fails composition instead of mid-run. Snapshots are computed on demand from the configuration and carry a content
/// fingerprint, so two reads of an unchanged profile are equal and a change is visible as a different fingerprint. The
/// registry is thread-safe.
/// </remarks>
internal sealed class GoalProfileRegistry
{
    private readonly ConcurrentDictionary<string, GoalProfileOptions> _profiles = new(StringComparer.Ordinal);
    private readonly AgentGoalOptions _host;

    /// <summary>Initializes an empty registry over one host ceiling set.</summary>
    /// <param name="host">The non-null host options, copied at construction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    internal GoalProfileRegistry(AgentGoalOptions host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = new AgentGoalOptions
        {
            MaximumDelegationDepth = host.MaximumDelegationDepth,
            MaximumChildrenPerGoal = host.MaximumChildrenPerGoal,
            MaximumConcurrentAttempts = host.MaximumConcurrentAttempts,
            DefaultJoinStrategy = host.DefaultJoinStrategy,
            FailureMode = host.FailureMode,
        };
    }

    /// <summary>Publishes the current snapshot of one profile.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="profile">The snapshot when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key is configured.</returns>
    internal bool TryGet(GoalProfileKey key, [NotNullWhen(true)] out GoalProfileSnapshot? profile)
    {
        profile = null;
        if (key.Value is not { Length: > 0 } value || !_profiles.TryGetValue(value, out var options))
        {
            return false;
        }

        profile = ToSnapshot(key, options);
        return true;
    }

    /// <summary>Applies one configuration callback.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="configure">The configuration callback.</param>
    /// <param name="replace">Whether to discard existing configuration first.</param>
    /// <exception cref="ArgumentException">The key is blank or a configured key is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A version or limit is invalid or exceeds a host ceiling.</exception>
    internal void Configure(GoalProfileKey key, Action<GoalProfileOptions> configure, bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        if (!replace && _profiles.TryGetValue(key.Value, out var existing))
        {
            configure(existing);
            Validate(existing);
            return;
        }

        var options = new GoalProfileOptions();
        configure(options);
        Validate(options);
        _profiles[key.Value] = options;
    }

    private static void ValidateLimit(int? value, int ceiling, string name)
    {
        if (value is { } limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, name);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, ceiling, name);
        }
    }

    private void Validate(GoalProfileOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Version.Value, nameof(options.Version));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StoreKey.Value, nameof(options.StoreKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DispatcherKey.Value, nameof(options.DispatcherKey));
        ValidateLimit(options.MaximumDelegationDepth, _host.MaximumDelegationDepth, nameof(options.MaximumDelegationDepth));
        ValidateLimit(options.MaximumChildrenPerGoal, _host.MaximumChildrenPerGoal, nameof(options.MaximumChildrenPerGoal));
        ValidateLimit(options.MaximumConcurrentAttempts, _host.MaximumConcurrentAttempts, nameof(options.MaximumConcurrentAttempts));
        foreach (var policy in options.PolicyIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Value, nameof(options.PolicyIds));
        }

        ArgumentException.ThrowIfNotEqual(options.JoinStrategies.Count > 0, true, nameof(options.JoinStrategies));
        foreach (var strategy in options.JoinStrategies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(strategy.Value, nameof(options.JoinStrategies));
        }

        if (options.DefaultJoinStrategy is { } defaultJoin)
        {
            ArgumentException.ThrowIfNotEqual(options.JoinStrategies.Contains(defaultJoin), true, nameof(options.DefaultJoinStrategy));
        }

        if (options.FailureMode is { } mode)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(mode, nameof(options.FailureMode));
        }
    }

    private GoalProfileSnapshot ToSnapshot(GoalProfileKey key, GoalProfileOptions options)
    {
        var depth = options.MaximumDelegationDepth ?? _host.MaximumDelegationDepth;
        var children = options.MaximumChildrenPerGoal ?? _host.MaximumChildrenPerGoal;
        var attempts = options.MaximumConcurrentAttempts ?? _host.MaximumConcurrentAttempts;
        var defaultJoin = options.DefaultJoinStrategy
            ?? (options.JoinStrategies.Contains(_host.DefaultJoinStrategy) ? _host.DefaultJoinStrategy : options.JoinStrategies[0]);
        var mode = options.FailureMode ?? _host.FailureMode;
        var root = options.RootBudget ?? new GoalBudget(100, 500, children);
        ImmutableArray<ComponentId> policies = [DenyUnlessAuthorizedDelegationPolicy.PolicyId, .. options.PolicyIds.Where(static id => id != DenyUnlessAuthorizedDelegationPolicy.PolicyId)];
        return new GoalProfileSnapshot(
            key,
            options.Version,
            options.StoreKey,
            options.DispatcherKey,
            policies,
            [.. options.JoinStrategies],
            defaultJoin,
            depth,
            children,
            attempts,
            mode,
            root,
            Fingerprint(key, options, policies, defaultJoin, depth, children, attempts, mode, root));
    }

    private static ContentHash Fingerprint(
        GoalProfileKey key,
        GoalProfileOptions options,
        ImmutableArray<ComponentId> policies,
        GoalJoinStrategyKey defaultJoin,
        int depth,
        int children,
        int attempts,
        DelegationFailureMode mode,
        GoalBudget root)
    {
        var builder = new StringBuilder();
        _ = builder.Append(key.Value).Append('\n')
            .Append(options.Version.Value.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(options.StoreKey.Value).Append('\n')
            .Append(options.DispatcherKey.Value).Append('\n')
            .Append(defaultJoin.Value).Append('\n')
            .Append(depth.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(children.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(attempts.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(((int) mode).ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(root.MaximumTurns.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(root.MaximumToolCalls.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(root.MaximumChildren.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var policy in policies)
        {
            _ = builder.Append("policy:").Append(policy.Value).Append('\n');
        }

        foreach (var strategy in options.JoinStrategies.Select(static item => item.Value).Order(StringComparer.Ordinal))
        {
            _ = builder.Append("join:").Append(strategy).Append('\n');
        }

        return new ContentHash($"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))}");
    }
}
