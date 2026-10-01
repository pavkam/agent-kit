// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Records one sink registration together with its coordinator and concrete type, so the dispatcher resolves exactly what was registered.</summary>
/// <param name="CoordinatorKey">The coordinator whose events the sink observes.</param>
/// <param name="Registration">The declared identity, order, and lifetime.</param>
/// <param name="SinkType">The concrete sink type.</param>
internal sealed record ArtifactEventSinkDeclaration(
    ComponentKey<IArtifactCoordinator> CoordinatorKey,
    ArtifactEventSinkRegistration Registration,
    Type SinkType);
