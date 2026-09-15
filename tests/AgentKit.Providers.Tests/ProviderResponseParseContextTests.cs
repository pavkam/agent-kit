// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies the value semantics and shared identity rule of <see cref="ProviderResponseParseContext"/>.</summary>
public sealed class ProviderResponseParseContextTests
{
    private static readonly ModelRequestId RequestId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly ProviderId Provider = new("provider");
    private static readonly ApiFamilyId ApiFamily = new("provider-chat");
    private static readonly ModelId RequestedModel = new("requested-model");
    private static readonly DeploymentId Deployment = new("deployment-1");
    private static readonly ProviderRequestId ProviderRequest = new("req_123");

    private static ProviderResponseParseContext CreateContext(DeploymentId? deploymentId = null, ProviderRequestId? providerRequestId = null) =>
        new(RequestId, Provider, ApiFamily, RequestedModel, deploymentId, providerRequestId);

    [Fact]
    public void Constructor_WhenAllValuesSupplied_ExposesThemUnchanged()
    {
        var context = CreateContext(Deployment, ProviderRequest);

        context.ModelRequestId.ShouldBe(RequestId);
        context.ProviderId.ShouldBe(Provider);
        context.ApiFamily.ShouldBe(ApiFamily);
        context.RequestedModelId.ShouldBe(RequestedModel);
        context.DeploymentId.ShouldBe(Deployment);
        context.ProviderRequestId.ShouldBe(ProviderRequest);
    }

    [Fact]
    public void Equals_WhenAllMembersMatch_IsTrue()
    {
        CreateContext(Deployment, ProviderRequest).ShouldBe(CreateContext(Deployment, ProviderRequest));
        CreateContext(Deployment, ProviderRequest).ShouldNotBe(CreateContext(Deployment, providerRequestId: null));
    }

    [Fact]
    public void CreateResponseIdentity_WhenBodyReportsModelAndResponseId_UsesBothAndCarriesContext()
    {
        var context = CreateContext(Deployment, ProviderRequest);

        var identity = context.CreateResponseIdentity("resolved-model", "resp_1");

        identity.ProviderId.ShouldBe(Provider);
        identity.UpstreamProviderId.ShouldBeNull();
        identity.ApiFamily.ShouldBe(ApiFamily);
        identity.RequestedModelId.ShouldBe(RequestedModel);
        identity.ResolvedModelId.ShouldBe(new ModelId("resolved-model"));
        identity.DeploymentId.ShouldBe(Deployment);
        identity.RequestId.ShouldBe(ProviderRequest);
        identity.ResponseId.ShouldBe(new ProviderResponseId("resp_1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateResponseIdentity_WhenBodyReportsNoModel_FallsBackToRequestedModel(string? resolvedModel)
    {
        var identity = CreateContext().CreateResponseIdentity(resolvedModel, "resp_1");

        identity.ResolvedModelId.ShouldBe(RequestedModel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateResponseIdentity_WhenBodyReportsNoResponseId_LeavesResponseIdNull(string? responseId)
    {
        var identity = CreateContext().CreateResponseIdentity("resolved-model", responseId);

        identity.ResponseId.ShouldBeNull();
    }

    [Fact]
    public void CreateResponseIdentity_WhenCalledWithoutArguments_UsesRequestedModelAndNoResponseId()
    {
        var identity = CreateContext(Deployment, ProviderRequest).CreateResponseIdentity();

        identity.ResolvedModelId.ShouldBe(RequestedModel);
        identity.ResponseId.ShouldBeNull();
        identity.DeploymentId.ShouldBe(Deployment);
        identity.RequestId.ShouldBe(ProviderRequest);
    }
}
