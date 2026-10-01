// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

internal sealed record ForeignToolCallRecordResult: ToolCallRecordResult
{
    internal ForeignToolCallRecordResult() { }
    internal ForeignToolCallRecordResult(ToolCallRecordResult original) : base(original) { }
}
