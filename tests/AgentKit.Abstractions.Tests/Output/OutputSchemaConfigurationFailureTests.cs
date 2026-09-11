// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaConfigurationFailure behavior and contracts.</summary>
public sealed class OutputSchemaConfigurationFailureTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter() => Should.Throw<ArgumentException>(() => new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "safe", [null!])).ParamName.ShouldBe("issues");
}
