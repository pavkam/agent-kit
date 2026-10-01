// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryOperationContext"/> constraints and identity agreement.</summary>
public sealed class MemoryOperationContextTests
{
    [Fact]
    public void Constructor_WhenEverythingAgrees_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var context = new MemoryOperationContext(
            owner.AgentId, owner.SessionId, owner.Identity, owner.Authorization.Scope.Correlation, owner.Authorization,
            new MemoryProfileKey("p"), new MemoryProfileVersion(3));

        context.AgentId.ShouldBe(owner.AgentId);
        context.SessionId.ShouldBe(owner.SessionId);
        context.Identity.ShouldBe(owner.Identity);
        context.Authorization.ShouldBe(owner.Authorization);
        context.ProfileKey.ShouldBe(new MemoryProfileKey("p"));
        context.ProfileVersion.ShouldBe(new MemoryProfileVersion(3));
    }

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => Create(owner, agent: default(AgentId))).ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenSessionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => Create(owner, session: default(SessionId))).ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenAuthorizationNamesAnotherAgent_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => Create(owner, agent: new AgentId(Guid.NewGuid()))).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationNamesAnotherSession_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => Create(owner, session: new SessionId(Guid.NewGuid()))).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationNamesAnotherIdentity_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var other = MemoryTestData.NewOwner(principal: "someone-else");

        Should.Throw<ArgumentException>(() => Create(owner, identity: other.Identity)).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenCorrelationDiffers_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var other = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), owner.RunId, null);

        Should.Throw<ArgumentException>(() => Create(owner, correlation: other)).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => Create(owner, identity: null, nullIdentity: true)).ParamName.ShouldBe("identity");
        Should.Throw<ArgumentNullException>(() => Create(owner, nullAuthorization: true)).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenProfileKeyIsDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => Create(owner, profile: default(MemoryProfileKey))).ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenProfileVersionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => Create(owner, version: default(MemoryProfileVersion))).ParamName.ShouldBe("profileVersion");
    }

    private static MemoryOperationContext Create(
        MemoryTestOwner owner,
        AgentId? agent = null,
        SessionId? session = null,
        ExecutionIdentity? identity = null,
        OperationCorrelation? correlation = null,
        MemoryProfileKey? profile = null,
        MemoryProfileVersion? version = null,
        bool nullIdentity = false,
        bool nullAuthorization = false) => new(
            agent ?? owner.AgentId,
            session ?? owner.SessionId,
            nullIdentity ? null! : identity ?? owner.Identity,
            correlation ?? owner.Authorization.Scope.Correlation,
            nullAuthorization ? null! : owner.Authorization,
            profile ?? new MemoryProfileKey("p"),
            version ?? new MemoryProfileVersion(1));
}
