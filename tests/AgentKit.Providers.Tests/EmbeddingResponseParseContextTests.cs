// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies the value semantics, derivability, and shared identity rule of <see cref="EmbeddingResponseParseContext"/>.</summary>
public sealed class EmbeddingResponseParseContextTests
{
    private static readonly EmbeddingRequestId RequestId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ProviderId Provider = new("provider");
    private static readonly ApiFamilyId ApiFamily = new("provider-embeddings");
    private static readonly ModelId RequestedModel = new("requested-embedding-model");
    private static readonly DeploymentId Deployment = new("deployment-1");
    private static readonly ProviderRequestId ProviderRequest = new("req_123");

    private static EmbeddingResponseParseContext CreateContext(DeploymentId? deploymentId = null, ProviderRequestId? providerRequestId = null) =>
        new(RequestId, Provider, ApiFamily, RequestedModel, deploymentId, providerRequestId);

    [Fact]
    public void Constructor_WhenAllValuesSupplied_ExposesThemUnchanged()
    {
        var context = CreateContext(Deployment, ProviderRequest);

        context.RequestId.ShouldBe(RequestId);
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
        CreateContext(Deployment, ProviderRequest).ShouldNotBe(CreateContext(deploymentId: null, ProviderRequest));
    }

    [Fact]
    public void WithExpression_WhenReplacingDeploymentId_ProducesIndependentCopy()
    {
        var original = CreateContext(deploymentId: null, ProviderRequest);

        var copy = original with { DeploymentId = Deployment };

        copy.DeploymentId.ShouldBe(Deployment);
        original.DeploymentId.ShouldBeNull();
        copy.ShouldNotBe(original);
    }

    [Fact]
    public void Equals_WhenDerivedRecordHasSameBaseMembers_IsNotEqualToBaseRecord()
    {
        var baseContext = CreateContext();
        var derived = new DerivedContext(RequestId, Provider, ApiFamily, RequestedModel, tag: "x");

        baseContext.ShouldNotBe(derived);
        derived.Tag.ShouldBe("x");
        derived.RequestId.ShouldBe(RequestId);
    }

    [Fact]
    public void CreateResponseIdentity_WhenBodyReportsModel_UsesItAndRetainsProviderRequestIdWhileOmittingResponseId()
    {
        var identity = CreateContext(Deployment, ProviderRequest).CreateResponseIdentity("resolved-embedding-model");

        identity.ProviderId.ShouldBe(Provider);
        identity.UpstreamProviderId.ShouldBeNull();
        identity.ApiFamily.ShouldBe(ApiFamily);
        identity.RequestedModelId.ShouldBe(RequestedModel);
        identity.ResolvedModelId.ShouldBe(new ModelId("resolved-embedding-model"));
        identity.DeploymentId.ShouldBe(Deployment);
        identity.RequestId.ShouldBe(ProviderRequest);
        identity.ResponseId.ShouldBeNull();
    }

    [Fact]
    public void CreateResponseIdentity_WhenNoProviderRequestIdSupplied_LeavesIdentityRequestIdNull()
    {
        var identity = CreateContext(Deployment, providerRequestId: null).CreateResponseIdentity("resolved-embedding-model");

        identity.RequestId.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateResponseIdentity_WhenBodyReportsNoModel_FallsBackToRequestedModel(string? resolvedModel)
    {
        // ModelId rejects a whitespace-only value; a provider that reports one must be treated the same
        // as reporting none instead of letting ModelId's constructor throw.
        var identity = CreateContext().CreateResponseIdentity(resolvedModel);

        identity.ResolvedModelId.ShouldBe(RequestedModel);
    }

    [Fact]
    public void CreateResponseIdentity_WhenCalledOnDerivedRecord_UsesInheritedMembers()
    {
        var derived = new DerivedContext(RequestId, Provider, ApiFamily, RequestedModel, tag: "x");

        var identity = derived.CreateResponseIdentity();

        identity.ProviderId.ShouldBe(Provider);
        identity.ResolvedModelId.ShouldBe(RequestedModel);
        identity.DeploymentId.ShouldBeNull();
    }

    private sealed record DerivedContext: EmbeddingResponseParseContext
    {
        public DerivedContext(EmbeddingRequestId requestId, ProviderId providerId, ApiFamilyId apiFamily, ModelId requestedModelId, string tag)
            : base(requestId, providerId, apiFamily, requestedModelId, deploymentId: null, providerRequestId: null) =>
            Tag = tag;

        public string Tag { get; }
    }
}
