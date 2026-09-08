// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Provides one durably accepted operation and its process-local ownership boundary.</summary>
/// <param name="Coordinator">The run coordinator under test.</param>
/// <param name="Request">The exact accepted-state ownership request.</param>
/// <param name="Session">The exact compiled invocation capability.</param>
/// <param name="AcceptedState">The canonical state loaded through the protected coordinator.</param>
public sealed record SessionRunCoordinatorConformanceScenario(
    ISessionRunCoordinator Coordinator,
    SessionRunLeaseRequest Request,
    SessionExecutionCapability Session,
    SessionAcceptedRunState AcceptedState);
