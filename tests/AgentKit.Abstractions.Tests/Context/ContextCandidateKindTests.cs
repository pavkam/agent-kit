// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextCandidateKind"/> values.</summary>
public sealed class ContextCandidateKindTests
{
    [Fact]
    public void ContextCandidateKind_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<ContextCandidateKind>().ShouldBe([ContextCandidateKind.Instruction, ContextCandidateKind.ReferenceData, ContextCandidateKind.RuntimeData]);
}
