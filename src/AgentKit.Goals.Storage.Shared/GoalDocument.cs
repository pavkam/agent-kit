// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of an <see cref="AgentGoal"/>.</summary>
/// <param name="Id">The goal identity.</param>
/// <param name="ParentId">The parent goal, or <see langword="null"/>.</param>
/// <param name="OwnerAgentId">The owning agent.</param>
/// <param name="SessionId">The owning session.</param>
/// <param name="OriginatingRunId">The creating run.</param>
/// <param name="ProfileKey">The captured profile key.</param>
/// <param name="ProfileVersion">The captured profile revision.</param>
/// <param name="AgentDefinitionRevision">The captured definition revision.</param>
/// <param name="Status">The status.</param>
/// <param name="Objective">The bounded objective.</param>
/// <param name="ContextReferences">The context references.</param>
/// <param name="Budget">The goal ceiling.</param>
/// <param name="ActiveAttemptId">The running attempt, or <see langword="null"/>.</param>
/// <param name="Version">The version token.</param>
/// <param name="CreatedAt">The creation instant.</param>
/// <param name="Extensions">The goal's extension data.</param>
/// <param name="DefinitionExtensions">The definition's extension data.</param>
internal sealed record GoalDocument(
    Guid Id,
    Guid? ParentId,
    Guid OwnerAgentId,
    Guid SessionId,
    Guid OriginatingRunId,
    string ProfileKey,
    long ProfileVersion,
    long AgentDefinitionRevision,
    GoalStatus Status,
    string Objective,
    ImmutableArray<string> ContextReferences,
    GoalBudgetDocument Budget,
    Guid? ActiveAttemptId,
    string Version,
    DateTimeOffset CreatedAt,
    ImmutableArray<GoalExtensionDocument> Extensions,
    ImmutableArray<GoalExtensionDocument> DefinitionExtensions)
{
    /// <summary>Converts a goal to its persisted form.</summary>
    /// <param name="value">The non-null goal.</param>
    /// <returns>The document.</returns>
    internal static GoalDocument FromDomain(AgentGoal value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.ParentId?.Value,
            value.OwnerAgentId.Value,
            value.SessionId.Value,
            value.OriginatingRunId.Value,
            value.ProfileKey.Value,
            value.ProfileVersion.Value,
            value.AgentDefinitionRevision.Value,
            value.Status,
            value.Definition.Objective,
            value.Definition.ContextReferences,
            GoalBudgetDocument.FromDomain(value.Budget),
            value.ActiveAttemptId?.Value,
            value.Version.Value,
            value.CreatedAt,
            GoalExtensionDocument.FromDomain(value.Extensions),
            GoalExtensionDocument.FromDomain(value.Definition.Extensions));
    }

    /// <summary>Restores the goal, re-running its validation.</summary>
    /// <returns>The goal.</returns>
    internal AgentGoal ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Budget);
        return new(
            new GoalId(Id),
            ParentId is { } parent ? new GoalId(parent) : null,
            new AgentId(OwnerAgentId),
            new SessionId(SessionId),
            new RunId(OriginatingRunId),
            new GoalProfileKey(ProfileKey),
            new GoalProfileVersion(ProfileVersion),
            new AgentDefinitionRevision(AgentDefinitionRevision),
            Status,
            new GoalDefinition(Objective, ContextReferences.IsDefault ? [] : ContextReferences, GoalExtensionDocument.ToDomain(DefinitionExtensions)),
            Budget.ToDomain(),
            ActiveAttemptId is { } attempt ? new GoalAttemptId(attempt) : null,
            new VersionToken(Version),
            CreatedAt,
            GoalExtensionDocument.ToDomain(Extensions));
    }
}
