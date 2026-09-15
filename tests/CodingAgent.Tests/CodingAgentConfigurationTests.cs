// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using System.Collections.Immutable;

using AgentKit;

/// <summary>Verifies the example captures validated model and sandbox composition choices.</summary>
public sealed class CodingAgentConfigurationTests
{
    [Fact]
    public void Models_WhenRead_ExposeRequestedOpenAIChoicesInStableOrder()
    {
        CodingAgentConfiguration.Models.Select(static option => option.ModelId)
            .ShouldBe(["gpt-5.6-terra", "gpt-5.6-sol", "gpt-6-astra"]);
    }

    [Fact]
    public void CreateDefault_WhenCalled_UsesTerraWithToolCompatibleReasoning()
    {
        var configuration = CodingAgentConfiguration.CreateDefault();

        configuration.ModelId.ShouldBe("gpt-5.6-terra");
        configuration.ReasoningEffort.ShouldBe(LlmReasoningEffort.None);
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_PreservesRuntimeChoices()
    {
        var roots = ImmutableArray.Create("/opt/toolchain");

        var configuration = new CodingAgentConfiguration(
            "gpt-6-astra",
            LlmReasoningEffort.ExtraHigh,
            18,
            roots);

        configuration.ModelId.ShouldBe("gpt-6-astra");
        configuration.ReasoningEffort.ShouldBe(LlmReasoningEffort.ExtraHigh);
        configuration.MaximumTurns.ShouldBe(18);
        configuration.ReadOnlyToolchainRoots.ShouldBe(roots);
    }
}
