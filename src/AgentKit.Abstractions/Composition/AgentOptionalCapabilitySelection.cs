// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects every optional per-agent capability this definition enables, or leaves each one absent.</summary>
/// <remarks>
/// <para>
/// Every field here names a demonstrated optional runtime axis: tool execution, artifact coordination, durable
/// execution, durable memory, and goal delegation are each independently enable-able, and enabling one never
/// implies another. A <see langword="null"/> field means this definition does not use that capability at all,
/// not that it uses an unconfigured default — an agent with no tools, no artifacts, no durability profile, no
/// memory profile, and no goal profile is a legitimate, fully specified composition.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over every field, including
/// <see cref="Capabilities"/> compared by sequence. It carries no mutable state and is safe to share and compare
/// across threads without synchronization.
/// </para>
/// </remarks>
public sealed record AgentOptionalCapabilitySelection
{
    /// <summary>Captures every optional capability selection, validating only the invariants a closed value can express.</summary>
    /// <param name="toolExecutor">The keyed <see cref="IToolExecutor"/> this agent uses, or <see langword="null"/> when this agent has no tools.</param>
    /// <param name="artifactCoordinator">The keyed <see cref="IArtifactCoordinator"/> this agent uses, or <see langword="null"/> when this agent produces no durable artifacts.</param>
    /// <param name="durabilityProfile">The durable-execution profile this agent uses, or <see langword="null"/> when runs are not durably checkpointed.</param>
    /// <param name="memoryProfile">The durable-memory profile this agent uses, or <see langword="null"/> when this agent has no durable memory.</param>
    /// <param name="goalProfile">The goal-and-delegation profile this agent uses, or <see langword="null"/> when this agent neither owns nor delegates goals.</param>
    /// <param name="capabilities">Initialized, nonnull declared external capability references this agent exposes to delegation or discovery.</param>
    /// <exception cref="ArgumentOutOfRangeException">A present optional key is a default, unusable value.</exception>
    /// <exception cref="ArgumentException"><paramref name="capabilities"/> is an uninitialized, default array, or contains a null entry.</exception>
    public AgentOptionalCapabilitySelection(
        ComponentKey<IToolExecutor>? toolExecutor,
        ComponentKey<IArtifactCoordinator>? artifactCoordinator,
        DurabilityProfileKey? durabilityProfile,
        MemoryProfileKey? memoryProfile,
        GoalProfileKey? goalProfile,
        ImmutableArray<AgentCapabilityReference> capabilities)
    {
        if (toolExecutor is { } toolExecutorKey) { ArgumentOutOfRangeException.ThrowIfEqual(toolExecutorKey, default, nameof(toolExecutor)); }
        if (artifactCoordinator is { } artifactCoordinatorKey) { ArgumentOutOfRangeException.ThrowIfEqual(artifactCoordinatorKey, default, nameof(artifactCoordinator)); }
        if (durabilityProfile is { } durability) { ArgumentOutOfRangeException.ThrowIfEqual(durability, default, nameof(durabilityProfile)); }
        if (memoryProfile is { } memory) { ArgumentOutOfRangeException.ThrowIfEqual(memory, default, nameof(memoryProfile)); }
        if (goalProfile is { } goal) { ArgumentOutOfRangeException.ThrowIfEqual(goal, default, nameof(goalProfile)); }
        ArgumentException.ThrowIfDefault(capabilities);
        ArgumentException.ThrowIfContainsNull(capabilities, nameof(capabilities));
        ToolExecutor = toolExecutor;
        ArtifactCoordinator = artifactCoordinator;
        DurabilityProfile = durabilityProfile;
        MemoryProfile = memoryProfile;
        GoalProfile = goalProfile;
        Capabilities = capabilities;
    }

    /// <summary>Gets the sole shared empty selection for an agent that enables no optional capability.</summary>
    /// <value>Every field absent or empty.</value>
    public static AgentOptionalCapabilitySelection None { get; } = new(null, null, null, null, null, []);

    /// <summary>Gets the keyed tool executor this agent uses.</summary>
    /// <value>A nondefault component key, or <see langword="null"/> when this agent has no tools.</value>
    public ComponentKey<IToolExecutor>? ToolExecutor { get; }

    /// <summary>Gets the keyed artifact coordinator this agent uses.</summary>
    /// <value>A nondefault component key, or <see langword="null"/> when this agent produces no durable artifacts.</value>
    public ComponentKey<IArtifactCoordinator>? ArtifactCoordinator { get; }

    /// <summary>Gets the durable-execution profile this agent uses.</summary>
    /// <value>A nondefault profile key, or <see langword="null"/> when runs are not durably checkpointed.</value>
    public DurabilityProfileKey? DurabilityProfile { get; }

    /// <summary>Gets the durable-memory profile this agent uses.</summary>
    /// <value>A nondefault profile key, or <see langword="null"/> when this agent has no durable memory.</value>
    public MemoryProfileKey? MemoryProfile { get; }

    /// <summary>Gets the goal-and-delegation profile this agent uses.</summary>
    /// <value>A nondefault profile key, or <see langword="null"/> when this agent neither owns nor delegates goals.</value>
    public GoalProfileKey? GoalProfile { get; }

    /// <summary>Gets the declared external capability references this agent exposes to delegation or discovery.</summary>
    /// <value>An initialized, nonnull immutable array; empty when this agent declares none.</value>
    public ImmutableArray<AgentCapabilityReference> Capabilities { get; }

    /// <summary>Compares every optional selection and the declared capability sequence structurally.</summary>
    /// <param name="other">The candidate selection, or null.</param>
    /// <returns>True only when every field, including capability order, agrees.</returns>
    public bool Equals(AgentOptionalCapabilitySelection? other) => other is not null
        && ToolExecutor == other.ToolExecutor && ArtifactCoordinator == other.ArtifactCoordinator
        && DurabilityProfile == other.DurabilityProfile && MemoryProfile == other.MemoryProfile
        && GoalProfile == other.GoalProfile && Capabilities.SequenceEqual(other.Capabilities);

    /// <summary>Hashes every optional selection consistently with structural equality.</summary>
    /// <returns>A hash over every field and the ordered capability sequence.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ToolExecutor); hash.Add(ArtifactCoordinator); hash.Add(DurabilityProfile);
        hash.Add(MemoryProfile); hash.Add(GoalProfile);
        foreach (var capability in Capabilities) { hash.Add(capability); }
        return hash.ToHashCode();
    }
}
