// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run completed at an idle boundary with no pending output decision or promoted work.</summary>
/// <remarks>This additive outcome fills a canonical lifecycle case absent from the reduced result family; broader outcome migration remains a compatibility-reviewed checkpoint.</remarks>
public sealed record AgentRunIdle: AgentRunOutcome;
