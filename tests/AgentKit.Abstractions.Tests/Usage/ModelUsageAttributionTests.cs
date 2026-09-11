// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies ModelUsageAttribution behavior and contracts.</summary>
public sealed class ModelUsageAttributionTests
{
    [Theory]
    [InlineData("requestId")]
    [InlineData("providerId")]
    [InlineData("apiFamily")]
    [InlineData("modelId")]
    [InlineData("deploymentId")]
    public void Constructor_WhenModelAttributionIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var model = RunUsageTests.Model;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelUsageAttribution(parameter == "requestId" ? default : model.RequestId, parameter == "providerId" ? default : model.ProviderId, parameter == "apiFamily" ? default : model.ApiFamily, parameter == "modelId" ? default : model.ModelId, parameter == "deploymentId" ? default(DeploymentId) : null));
        exception.ParamName.ShouldBe(parameter);
    }
}
