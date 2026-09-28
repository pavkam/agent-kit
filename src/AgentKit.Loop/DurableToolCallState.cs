// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>The journaled manifest of one requested tool call's invocation.</summary>
/// <param name="TurnId">The turn that requested the call.</param>
/// <param name="CallId">The requested call identity the terminal result must correlate to.</param>
/// <param name="ToolAlias">The alias the model requested, preserved exactly as requested.</param>
/// <remarks>
/// Each requested call is journaled separately because each reaches exactly one terminal result, and a batch-level
/// record could not say which call's effect had run. Tool arguments and results are content and never appear here.
/// </remarks>
internal sealed record DurableToolCallState(Guid TurnId, string CallId, string ToolAlias);
