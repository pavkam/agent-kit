// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Marks one keyed coordinator registration so a repeated registration is idempotent and a conflicting one is rejected.</summary>
/// <param name="Key">The coordinator key.</param>
/// <param name="ProfileKey">The logical profile the coordinator is bound to.</param>
/// <param name="Options">The captured coordinator mechanics.</param>
internal sealed record ArtifactCoordinatorRegistration(
    ComponentKey<IArtifactCoordinator> Key,
    ArtifactProfileKey ProfileKey,
    AgentArtifactOptionsSnapshot Options);
