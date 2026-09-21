// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextCandidate"/> invariants.</summary>
public sealed class ContextCandidateTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesEvidence() => ContextTestData.Candidate().Kind.ShouldBe(ContextCandidateKind.Instruction);

    [Fact]
    public void Constructor_WhenSourceIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => new ContextCandidate(
            null!,
            ContextCandidateKind.Instruction,
            ContextTrust.AgentDefinition,
            0,
            ContextScope.ModelRequest,
            new ContextCostEstimate(1, null),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerModelRequest,
            false,
            [],
            ExtensionData.Empty)).ParamName.ShouldBe("source");

    [Fact]
    public void Constructor_WhenKindIsUndefined_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ContextCandidate(
            ContextTestData.Source(),
            (ContextCandidateKind) 99,
            ContextTrust.AgentDefinition,
            0,
            ContextScope.ModelRequest,
            new ContextCostEstimate(1, null),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerModelRequest,
            false,
            [],
            ExtensionData.Empty)).ParamName.ShouldBe("kind");
}
