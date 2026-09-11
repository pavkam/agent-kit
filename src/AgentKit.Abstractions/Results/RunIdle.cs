// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports semantic completion at an idle boundary with no pending work.</summary>
/// <remarks>The operation owner revalidates the idle boundary; this value is not a settlement receipt.</remarks>
public sealed record RunIdle: AgentRunOutcome;
