// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports successful semantic completion after output validation.</summary>
/// <remarks>This outcome carries no settlement claim; clean success also requires RunSettlementCompleted.</remarks>
public sealed record RunSucceeded: AgentRunOutcome;
