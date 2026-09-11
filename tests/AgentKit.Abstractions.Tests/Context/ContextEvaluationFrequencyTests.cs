// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit;

/// <summary>Verifies ContextEvaluationFrequency behavior and contracts.</summary>
public sealed class ContextEvaluationFrequencyTests
{
    [Fact]
    public void ContextEnums_WhenEnumerated_ContainOnlyNormativeValues() => Enum.GetValues<ContextEvaluationFrequency>().ShouldBe([ContextEvaluationFrequency.OncePerRun, ContextEvaluationFrequency.OncePerModelRequest,]);
}
