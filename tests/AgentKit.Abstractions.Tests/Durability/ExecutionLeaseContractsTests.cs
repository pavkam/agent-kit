// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises lease request and outcome guards, including the rule that a
/// non-positive lease duration is expired the instant it is granted.
/// </summary>
public sealed class ExecutionLeaseContractsTests
{
    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExecutionLeaseRequest(
                null!,
                DurabilityTestData.WorkerId,
                TimeSpan.FromSeconds(30)));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenWorkerIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseRequest(
                DurabilityTestData.Address(),
                default,
                TimeSpan.FromSeconds(30)));

        exception.ParamName.ShouldBe("workerId");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenDurationIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseRequest(
                DurabilityTestData.Address(),
                DurabilityTestData.WorkerId,
                TimeSpan.Zero));

        exception.ParamName.ShouldBe("duration");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenDurationIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseRequest(
                DurabilityTestData.Address(),
                DurabilityTestData.WorkerId,
                TimeSpan.FromSeconds(-1)));

        exception.ParamName.ShouldBe("duration");
    }

    [Fact]
    public void ExecutionLeaseRequest_With_WhenDurationIsZero_Throws()
    {
        var request = new ExecutionLeaseRequest(
            DurabilityTestData.Address(),
            DurabilityTestData.WorkerId,
            TimeSpan.FromSeconds(30));

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => request with { Duration = TimeSpan.Zero });

        exception.ParamName.ShouldBe(nameof(ExecutionLeaseRequest.Duration));
    }

    [Fact]
    public void ExecutionLeaseAcquired_Constructor_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExecutionLeaseAcquired(null!));

        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void ExecutionLeaseHeldByAnotherWorker_Constructor_WhenOwnerIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseHeldByAnotherWorker(
                default,
                DurabilityTestData.Token,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("currentOwnerWorkerId");
    }

    [Fact]
    public void ExecutionLeaseHeldByAnotherWorker_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseHeldByAnotherWorker(
                DurabilityTestData.WorkerId,
                default,
                DurabilityTestData.Now));

        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void LeaseLost_Constructor_WhenTokenOmitted_CurrentTokenIsNull() =>
        new LeaseLost().CurrentToken.ShouldBeNull();

    [Fact]
    public void LeaseLost_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new LeaseLost(default(FencingToken)));

        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void LeaseRenewed_Constructor_PreservesExtendedExpiry() =>
        new LeaseRenewed(DurabilityTestData.Now).ExpiresAt.ShouldBe(DurabilityTestData.Now);

    [Fact]
    public void ExecutionLeaseResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(ExecutionLeaseResult).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ExecutionLeaseResult)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe([
            nameof(ExecutionLeaseAcquired),
            nameof(ExecutionLeaseHeldByAnotherWorker),
        ]);
    }

    [Fact]
    public void LeaseRenewalResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(LeaseRenewalResult).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(LeaseRenewalResult)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe([nameof(LeaseLost), nameof(LeaseRenewed)]);
    }
}
