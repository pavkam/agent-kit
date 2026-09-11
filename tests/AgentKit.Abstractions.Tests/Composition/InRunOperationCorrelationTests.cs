// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

/// <summary>Verifies InRunOperationCorrelation behavior and contracts.</summary>
public sealed class InRunOperationCorrelationTests
{
    [Fact]
    public void InRunOperationCorrelation_Equality_WhenSameValues_InstancesAreEqual()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        new InRunOperationCorrelation(operationId, runId, null).ShouldBe(new InRunOperationCorrelation(operationId, runId, null));
    }

    [Fact]
    public void InRunOperationCorrelation_Constructor_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        correlation.OperationId.ShouldBe(operationId);
        correlation.RunId.ShouldBe(runId);
        correlation.TurnId.ShouldBe(turnId);
    }

    /// <summary>Verifies canonical scope and correlation values reject default nested identities, including record-copy mutation.</summary>
    [Fact]
    public void Constructor_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new InRunOperationCorrelation(OperationId(), default, null)).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => new InRunOperationCorrelation(OperationId(), RunId(), default(TurnId))).ParamName.ShouldBe("turnId");
    }

    /// <summary>Verifies every hardened init accessor rejects invalid record copies without changing the original.</summary>
    [Fact]
    public void With_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgumentAndPreservesOriginal()
    {
        var during = new InRunOperationCorrelation(OperationId(), RunId(), new TurnId(Guid.Parse("77777777-7777-7777-7777-777777777777")));
        Should.Throw<ArgumentOutOfRangeException>(() => during with { RunId = default }).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => during with { TurnId = default(TurnId) }).ParamName.ShouldBe("turnId");
        _ = during.TurnId.ShouldNotBeNull();
    }

    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static RunId RunId() => new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
}
