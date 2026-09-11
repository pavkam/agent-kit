// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

/// <summary>Verifies AfterRunOperationCorrelation behavior and contracts.</summary>
public sealed class AfterRunOperationCorrelationTests
{
    [Fact]
    public void AfterRunOperationCorrelation_Constructor_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var correlation = new AfterRunOperationCorrelation(operationId, runId);
        correlation.OperationId.ShouldBe(operationId);
        correlation.CausalRunId.ShouldBe(runId);
    }

    [Fact]
    public void AfterRunOperationCorrelation_Equality_WhenSameValues_InstancesAreEqual()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        new AfterRunOperationCorrelation(operationId, runId).ShouldBe(new AfterRunOperationCorrelation(operationId, runId));
    }

    /// <summary>Verifies canonical scope and correlation values reject default nested identities, including record-copy mutation.</summary>
    [Fact]
    public void Constructor_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgument() => Should.Throw<ArgumentOutOfRangeException>(() => new AfterRunOperationCorrelation(OperationId(), default)).ParamName.ShouldBe("causalRunId");

    /// <summary>Verifies every hardened init accessor rejects invalid record copies without changing the original.</summary>
    [Fact]
    public void With_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgumentAndPreservesOriginal()
    {
        var after = new AfterRunOperationCorrelation(OperationId(), RunId());
        Should.Throw<ArgumentOutOfRangeException>(() => after with { CausalRunId = default }).ParamName.ShouldBe("causalRunId");
        after.CausalRunId.ShouldBe(RunId());
    }

    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static RunId RunId() => new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
}
