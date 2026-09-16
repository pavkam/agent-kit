// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelSelectionRequest behavior and contracts.</summary>
public sealed class ModelSelectionRequestTests
{
    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelectionRequest(null!, ModelRequestId(), Policy(), ModelRequirements.None, Catalog())).ParamName.ShouldBe("scope");

    [Fact]
    public void Constructor_WhenModelRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionRequest(Scope(), default, Policy(), ModelRequirements.None, Catalog())).ParamName.ShouldBe("modelRequestId");

    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelectionRequest(Scope(), ModelRequestId(), null!, ModelRequirements.None, Catalog())).ParamName.ShouldBe("policy");

    [Fact]
    public void Constructor_WhenRequirementsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelectionRequest(Scope(), ModelRequestId(), Policy(), null!, Catalog())).ParamName.ShouldBe("requirements");

    [Fact]
    public void Constructor_WhenCatalogIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelectionRequest(Scope(), ModelRequestId(), Policy(), ModelRequirements.None, null!)).ParamName.ShouldBe("catalog");

    [Fact]
    public void Constructor_WhenTurnIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionRequest(Scope(), ModelRequestId(), Policy(), ModelRequirements.None, Catalog(), default(TurnId))).ParamName.ShouldBe("turnId");

    [Fact]
    public void Initializer_WhenScopeIsNull_ThrowsExactArgumentNullException()
    {
        var request = Request();
        Should.Throw<ArgumentNullException>(() => request with { Scope = null! }).ParamName.ShouldBe("Scope");
    }

    [Fact]
    public void Initializer_WhenModelRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var request = Request();
        Should.Throw<ArgumentOutOfRangeException>(() => request with { ModelRequestId = default }).ParamName.ShouldBe("ModelRequestId");
    }

    [Fact]
    public void Initializer_WhenPolicyIsNull_ThrowsExactArgumentNullException()
    {
        var request = Request();
        Should.Throw<ArgumentNullException>(() => request with { Policy = null! }).ParamName.ShouldBe("Policy");
    }

    [Fact]
    public void Initializer_WhenRequirementsIsNull_ThrowsExactArgumentNullException()
    {
        var request = Request();
        Should.Throw<ArgumentNullException>(() => request with { Requirements = null! }).ParamName.ShouldBe("Requirements");
    }

    [Fact]
    public void Initializer_WhenCatalogIsNull_ThrowsExactArgumentNullException()
    {
        var request = Request();
        Should.Throw<ArgumentNullException>(() => request with { Catalog = null! }).ParamName.ShouldBe("Catalog");
    }

    [Fact]
    public void Initializer_WhenTurnIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var request = Request();
        Should.Throw<ArgumentOutOfRangeException>(() => request with { TurnId = default(TurnId) }).ParamName.ShouldBe("TurnId");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var scope = Scope();
        var modelRequestId = ModelRequestId();
        var policy = Policy();
        var catalog = Catalog();
        var turnId = new TurnId(Guid.NewGuid());
        var request = new ModelSelectionRequest(scope, modelRequestId, policy, ModelRequirements.None, catalog, turnId);
        request.Scope.ShouldBe(scope);
        request.ModelRequestId.ShouldBe(modelRequestId);
        request.Policy.ShouldBe(policy);
        request.Requirements.ShouldBe(ModelRequirements.None);
        request.Catalog.ShouldBe(catalog);
        request.TurnId.ShouldBe(turnId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SecurityAuthorizationScope Scope() =>
        new(new AgentId(Guid.NewGuid()), null, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));

    private static ModelRequestId ModelRequestId() => new(Guid.NewGuid());
    private static ModelSelectionPolicy Policy() => new([new ModelAlias("chat")]);
    private static ModelCatalogSnapshot Catalog() => new(new ModelCatalogVersion(1), [ProvidersTestData.Descriptor()]);
    private static ModelSelectionRequest Request() => new(Scope(), ModelRequestId(), Policy(), ModelRequirements.None, Catalog());
}
