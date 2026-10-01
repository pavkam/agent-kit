// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

internal sealed record ForeignToolExecutionPlanResult: ToolExecutionPlanResult
{
    internal ForeignToolExecutionPlanResult() { }
    internal ForeignToolExecutionPlanResult(ToolExecutionPlanResult original) : base(original) { }
}
