// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaCandidateInvalid behavior and contracts.</summary>
public sealed class OutputSchemaCandidateInvalidTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter()
    {
        Should.Throw<ArgumentException>(() => new OutputSchemaCandidateInvalid([])).ParamName.ShouldBe("issues");
        Should.Throw<ArgumentException>(() => new OutputSchemaCandidateInvalid([null!])).ParamName.ShouldBe("issues");
    }
}
