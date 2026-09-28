// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The terminal payload a journaled in-process boundary records.</summary>
/// <param name="OperationName">The nonblank boundary name whose effect attempt completed.</param>
/// <remarks>
/// A boundary's semantic outcome lives in session records, approval records, and the run result, all of which are
/// authoritative and already durable. The durable terminal payload therefore records only that this boundary's
/// effect attempt reached its end, which is the fact recovery needs and the only fact that is free of model, tool,
/// or retrieved content.
/// </remarks>
public sealed record DurableBoundaryOutcome(string OperationName);
