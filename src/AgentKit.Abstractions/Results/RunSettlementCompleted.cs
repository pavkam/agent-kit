// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports completion of the configured bounded settlement protocol.</summary>
/// <remarks>The owner constructs this only after required persistence and publication intents reach their selected boundary; the value itself performs no effects.</remarks>
public sealed record RunSettlementCompleted: RunSettlementOutcome;
