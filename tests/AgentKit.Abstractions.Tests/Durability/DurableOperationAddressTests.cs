// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises the durable operation address's identity guards, including the
/// rule that an absent turn is null rather than an empty identity.
/// </summary>
public sealed class DurableOperationAddressTests
{
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationAddress(
                default,
                DurabilityTestData.SessionId,
                DurabilityTestData.RunId,
                DurabilityTestData.OperationId));

        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationAddress(
                DurabilityTestData.AgentId,
                default,
                DurabilityTestData.RunId,
                DurabilityTestData.OperationId));

        exception.ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenRunIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationAddress(
                DurabilityTestData.AgentId,
                DurabilityTestData.SessionId,
                default,
                DurabilityTestData.OperationId));

        exception.ParamName.ShouldBe("runId");
    }

    [Fact]
    public void Constructor_WhenOperationIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationAddress(
                DurabilityTestData.AgentId,
                DurabilityTestData.SessionId,
                DurabilityTestData.RunId,
                default));

        exception.ParamName.ShouldBe("operationId");
    }

    [Fact]
    public void Constructor_WhenTurnIdIsOmitted_RepresentsBetweenTurnWork()
    {
        var address = new DurableOperationAddress(
            DurabilityTestData.AgentId,
            DurabilityTestData.SessionId,
            DurabilityTestData.RunId,
            DurabilityTestData.OperationId);

        address.TurnId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenTurnIdIsEmptyIdentity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableOperationAddress(
                DurabilityTestData.AgentId,
                DurabilityTestData.SessionId,
                DurabilityTestData.RunId,
                DurabilityTestData.OperationId,
                default(TurnId)));

        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void Constructor_WhenTurnIdIsSupplied_PreservesIt() =>
        DurabilityTestData.Address().TurnId.ShouldBe(DurabilityTestData.TurnId);

    [Fact]
    public void With_WhenIdentityIsDefault_ThrowsBeforeProducingInvalidAddress()
    {
        var address = DurabilityTestData.Address();

        Should.Throw<ArgumentOutOfRangeException>(() => address with { AgentId = default })
            .ParamName.ShouldBe(nameof(DurableOperationAddress.AgentId));
        Should.Throw<ArgumentOutOfRangeException>(() => address with { SessionId = default })
            .ParamName.ShouldBe(nameof(DurableOperationAddress.SessionId));
        Should.Throw<ArgumentOutOfRangeException>(() => address with { RunId = default })
            .ParamName.ShouldBe(nameof(DurableOperationAddress.RunId));
        Should.Throw<ArgumentOutOfRangeException>(() => address with { OperationId = default })
            .ParamName.ShouldBe(nameof(DurableOperationAddress.OperationId));
        Should.Throw<ArgumentOutOfRangeException>(
                () => address with { TurnId = default(TurnId) })
            .ParamName.ShouldBe(nameof(DurableOperationAddress.TurnId));
    }

    [Fact]
    public void With_WhenTurnIdIsClearedToNull_Succeeds() =>
        (DurabilityTestData.Address() with { TurnId = null }).TurnId.ShouldBeNull();

    [Fact]
    public void With_WhenIdentityIsValid_UpdatesProperty()
    {
        var address = DurabilityTestData.Address();
        var newAgentId = new AgentId(Guid.Parse("f7000000-0000-0000-0000-000000000016"));
        var newSessionId = new SessionId(Guid.Parse("f8000000-0000-0000-0000-000000000017"));
        var newRunId = new RunId(Guid.Parse("f9000000-0000-0000-0000-000000000018"));
        var newOperationId = new OperationId(Guid.Parse("fa000000-0000-0000-0000-000000000019"));

        (address with { AgentId = newAgentId }).AgentId.ShouldBe(newAgentId);
        (address with { SessionId = newSessionId }).SessionId.ShouldBe(newSessionId);
        (address with { RunId = newRunId }).RunId.ShouldBe(newRunId);
        (address with { OperationId = newOperationId }).OperationId.ShouldBe(newOperationId);
    }

    [Fact]
    public void Equality_WhenSameCoordinates_InstancesAreEqual() =>
        DurabilityTestData.Address().ShouldBe(DurabilityTestData.Address());
}
