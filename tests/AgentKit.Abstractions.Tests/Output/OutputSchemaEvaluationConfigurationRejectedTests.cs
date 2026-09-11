// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaEvaluationConfigurationRejected behavior and contracts.</summary>
public sealed class OutputSchemaEvaluationConfigurationRejectedTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter() => Should.Throw<ArgumentNullException>(() => new OutputSchemaEvaluationConfigurationRejected(null!)).ParamName.ShouldBe("failure");
}
