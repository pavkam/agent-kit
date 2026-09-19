// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolCallRequest behavior and contracts.</summary>
public sealed class ToolCallRequestTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var authorization = Authorization();
        var request = new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, authorization, CatalogVersion(),
            3, ProviderAlias(), [1, 2, 3], DateTimeOffset.UnixEpoch);

        request.AgentId.ShouldBe(TestAgentId);
        request.SessionId.ShouldBe(TestSessionId);
        request.RunId.ShouldBe(TestRunId);
        request.TurnId.ShouldBe(TestTurnId);
        request.OperationId.ShouldBe(TestOperationId);
        request.CallId.ShouldBe(CallId);
        request.Authorization.ShouldBe(authorization);
        request.CatalogVersion.ShouldBe(CatalogVersion());
        request.SourceOrdinal.ShouldBe(3);
        request.ProviderAlias.ShouldBe(ProviderAlias());
        ImmutableArray<byte> expectedArguments = [1, 2, 3];
        request.RawArguments.ShouldBe(expectedArguments);
        request.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, null!, CatalogVersion(),
            0, ProviderAlias(), [1], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenAuthorizationDoesNotMatchIdentity_ThrowsExactParameter()
    {
        var wrongAgentId = new AgentId(Guid.NewGuid());
        var exception = Should.Throw<ArgumentException>(() => new ToolCallRequest(
            wrongAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
            0, ProviderAlias(), [1], DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenCatalogVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), default,
            0, ProviderAlias(), [1], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("catalogVersion");

    [Fact]
    public void Constructor_WhenSourceOrdinalIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
            -1, ProviderAlias(), [1], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("sourceOrdinal");

    [Fact]
    public void Constructor_WhenProviderAliasIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
            0, default, [1], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("providerAlias");

    [Fact]
    public void Constructor_WhenRawArgumentsIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
            0, ProviderAlias(), default, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("rawArguments");

    [Fact]
    public void Equals_WhenRawArgumentsDiffer_ReturnsFalse()
    {
        var first = CallRequest();
        var second = new ToolCallRequest(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
            0, ProviderAlias(), [9, 9, 9], DateTimeOffset.UnixEpoch);
        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = CallRequest();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
