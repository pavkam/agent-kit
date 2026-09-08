// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Provides two accepted operations whose ownership partitions must remain independent.</summary>
/// <param name="First">The first accepted operation.</param>
/// <param name="Second">The second accepted operation sharing only the coordinates selected by the fixture method.</param>
public sealed record SessionRunCoordinatorConformancePair(
    SessionRunCoordinatorConformanceScenario First,
    SessionRunCoordinatorConformanceScenario Second);
