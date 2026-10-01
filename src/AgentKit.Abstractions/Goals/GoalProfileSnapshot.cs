// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the immutable, validated configuration published for one versioned goal profile.</summary>
/// <remarks>
/// A snapshot names the store, dispatcher, policy set, join strategies, limits, and budget a goal profile uses. It is
/// captured by key and version on every durable goal, so resume and delayed joins never reinterpret work under a newer
/// profile. Limits here are the profile's ceilings; a parent or run may reserve less but never more.
/// </remarks>
public sealed record GoalProfileSnapshot
{
    /// <summary>Initializes a validated snapshot.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="version">The positive published revision.</param>
    /// <param name="storeKey">The goal store this profile persists to.</param>
    /// <param name="dispatcherKey">The dispatcher this profile hands children to.</param>
    /// <param name="policyIds">The delegation policies this profile evaluates; empty selects none.</param>
    /// <param name="joinStrategies">The join strategies parents may declare; non-empty and containing the default.</param>
    /// <param name="defaultJoinStrategy">The join strategy used when a request names none.</param>
    /// <param name="maximumDelegationDepth">The positive delegation depth ceiling.</param>
    /// <param name="maximumChildrenPerGoal">The positive child-count ceiling per goal.</param>
    /// <param name="maximumConcurrentAttempts">The positive concurrent-attempt ceiling.</param>
    /// <param name="failureMode">The defined sibling-failure behavior.</param>
    /// <param name="rootBudget">The ceiling for a run's root goal.</param>
    /// <param name="fingerprint">The canonical content fingerprint of the published configuration.</param>
    /// <exception cref="ArgumentException">A key or fingerprint is blank, an array is default, empty, or contains a blank entry, or the default join is not listed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version or a ceiling is not positive, or the failure mode is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rootBudget"/> is null.</exception>
    public GoalProfileSnapshot(
        GoalProfileKey key,
        GoalProfileVersion version,
        GoalStoreKey storeKey,
        DelegationDispatcherKey dispatcherKey,
        ImmutableArray<ComponentId> policyIds,
        ImmutableArray<GoalJoinStrategyKey> joinStrategies,
        GoalJoinStrategyKey defaultJoinStrategy,
        int maximumDelegationDepth,
        int maximumChildrenPerGoal,
        int maximumConcurrentAttempts,
        DelegationFailureMode failureMode,
        GoalBudget rootBudget,
        ContentHash fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey.Value, nameof(storeKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherKey.Value, nameof(dispatcherKey));
        ArgumentException.ThrowIfDefault(policyIds);
        foreach (var policy in policyIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Value, nameof(policyIds));
        }

        ArgumentException.ThrowIfDefaultOrEmpty(joinStrategies);
        foreach (var strategy in joinStrategies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(strategy.Value, nameof(joinStrategies));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(defaultJoinStrategy.Value, nameof(defaultJoinStrategy));
        ArgumentException.ThrowIfNotEqual(joinStrategies.Contains(defaultJoinStrategy), true, nameof(defaultJoinStrategy));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDelegationDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumChildrenPerGoal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentAttempts);
        ArgumentOutOfRangeException.ThrowIfUndefined(failureMode);
        ArgumentNullException.ThrowIfNull(rootBudget);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint.Value, nameof(fingerprint));
        Key = key;
        Version = version;
        StoreKey = storeKey;
        DispatcherKey = dispatcherKey;
        PolicyIds = policyIds;
        JoinStrategies = joinStrategies;
        DefaultJoinStrategy = defaultJoinStrategy;
        MaximumDelegationDepth = maximumDelegationDepth;
        MaximumChildrenPerGoal = maximumChildrenPerGoal;
        MaximumConcurrentAttempts = maximumConcurrentAttempts;
        FailureMode = failureMode;
        RootBudget = rootBudget;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the profile key.</summary>
    public GoalProfileKey Key { get; }

    /// <summary>Gets the published revision.</summary>
    public GoalProfileVersion Version { get; }

    /// <summary>Gets the goal store this profile persists to.</summary>
    public GoalStoreKey StoreKey { get; }

    /// <summary>Gets the dispatcher this profile hands children to.</summary>
    public DelegationDispatcherKey DispatcherKey { get; }

    /// <summary>Gets the delegation policies this profile evaluates.</summary>
    public ImmutableArray<ComponentId> PolicyIds { get; }

    /// <summary>Gets the join strategies parents may declare.</summary>
    public ImmutableArray<GoalJoinStrategyKey> JoinStrategies { get; }

    /// <summary>Gets the default join strategy.</summary>
    public GoalJoinStrategyKey DefaultJoinStrategy { get; }

    /// <summary>Gets the delegation depth ceiling.</summary>
    public int MaximumDelegationDepth { get; }

    /// <summary>Gets the child-count ceiling per goal.</summary>
    public int MaximumChildrenPerGoal { get; }

    /// <summary>Gets the concurrent-attempt ceiling.</summary>
    public int MaximumConcurrentAttempts { get; }

    /// <summary>Gets the sibling-failure behavior.</summary>
    public DelegationFailureMode FailureMode { get; }

    /// <summary>Gets the ceiling for a run's root goal.</summary>
    public GoalBudget RootBudget { get; }

    /// <summary>Gets the canonical content fingerprint of the published configuration.</summary>
    public ContentHash Fingerprint { get; }

    /// <summary>Gets the reference that names exactly this published profile.</summary>
    public GoalProfileReference Reference => new(Key, Version);

    /// <inheritdoc/>
    public bool Equals(GoalProfileSnapshot? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && StoreKey == other.StoreKey
        && DispatcherKey == other.DispatcherKey
        && PolicyIds.SequenceEqual(other.PolicyIds)
        && JoinStrategies.SequenceEqual(other.JoinStrategies)
        && DefaultJoinStrategy == other.DefaultJoinStrategy
        && MaximumDelegationDepth == other.MaximumDelegationDepth
        && MaximumChildrenPerGoal == other.MaximumChildrenPerGoal
        && MaximumConcurrentAttempts == other.MaximumConcurrentAttempts
        && FailureMode == other.FailureMode
        && RootBudget == other.RootBudget
        && Fingerprint == other.Fingerprint;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(StoreKey);
        hash.Add(DispatcherKey);
        foreach (var policy in PolicyIds)
        {
            hash.Add(policy);
        }

        foreach (var strategy in JoinStrategies)
        {
            hash.Add(strategy);
        }

        hash.Add(DefaultJoinStrategy);
        hash.Add(MaximumDelegationDepth);
        hash.Add(MaximumChildrenPerGoal);
        hash.Add(MaximumConcurrentAttempts);
        hash.Add(FailureMode);
        hash.Add(RootBudget);
        hash.Add(Fingerprint);
        return hash.ToHashCode();
    }
}
