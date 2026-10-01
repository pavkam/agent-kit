// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Names the bounded, content-free identity of one observed coordinator operation.</summary>
/// <param name="Logger">The coordinator logger.</param>
/// <param name="Time">The clock used only for observational duration.</param>
/// <param name="CoordinatorKey">The coordinator that runs the operation.</param>
/// <param name="ProfileKey">The logical profile the coordinator is bound to.</param>
/// <param name="ActivityName">The shared activity name for the operation.</param>
/// <param name="Operation">The bounded operation label.</param>
/// <param name="TenantId">The tenant, when established.</param>
/// <param name="ArtifactId">The artifact identity, when established.</param>
/// <param name="PreparationId">The preparation identity, when established.</param>
internal sealed record ArtifactObservationTarget(
    ILogger Logger,
    TimeProvider Time,
    ComponentKey<IArtifactCoordinator> CoordinatorKey,
    ArtifactProfileKey ProfileKey,
    string ActivityName,
    string Operation,
    TenantId? TenantId,
    ArtifactId? ArtifactId,
    ArtifactPreparationId? PreparationId);
