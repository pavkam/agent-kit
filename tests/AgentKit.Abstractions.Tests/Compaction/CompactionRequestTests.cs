// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

public sealed class CompactionRequestTests
{
    public static TheoryData<double> InvalidReductionRatios =>
    [
        double.NaN,
        double.NegativeInfinity,
        -1d,
        0d,
        1d,
        2d,
        double.PositiveInfinity,
    ];

    [Theory]
    [MemberData(nameof(InvalidReductionRatios))]
    public void Constructor_WhenMinimumReductionRatioIsInvalid_ThrowsArgumentOutOfRangeException(double ratio)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => CreateRequest(ratio));

        exception.ParamName.ShouldBe("minimumReductionRatio");
    }

    [Theory]
    [MemberData(nameof(InvalidReductionRatios))]
    public void With_WhenMinimumReductionRatioIsInvalid_ThrowsArgumentOutOfRangeException(double ratio)
    {
        var request = CreateRequest(0.5d);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => request with { MinimumReductionRatio = ratio });

        exception.ParamName.ShouldBe(nameof(CompactionRequest.MinimumReductionRatio));
    }

    [Fact]
    public void ConstructorAndWith_WhenMinimumReductionRatioIsAtValidBoundaries_AcceptsValues()
    {
        var minimum = double.Epsilon;
        var maximum = Math.BitDecrement(1d);

        var request = CreateRequest(minimum);
        var changed = request with { MinimumReductionRatio = maximum };

        request.MinimumReductionRatio.ShouldBe(minimum);
        changed.MinimumReductionRatio.ShouldBe(maximum);
    }

    [Fact]
    public void Constructor_WhenDeadlineDoesNotFollowRequestedAt_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateRequest(0.5d, deadline: DateTimeOffset.UnixEpoch));

        exception.ParamName.ShouldBe("deadline");
    }

    [Fact]
    public void With_WhenMinimumRetainedEntriesNegative_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest(0.5d);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { MinimumRetainedEntries = -1 });

        exception.ParamName.ShouldBe(nameof(CompactionRequest.MinimumRetainedEntries));
    }

    [Fact]
    public void With_WhenMinimumRetainedEntriesZero_AcceptsValue()
    {
        var request = CreateRequest(0.5d);

        var changed = request with { MinimumRetainedEntries = 0 };

        changed.MinimumRetainedEntries.ShouldBe(0);
    }

    [Fact]
    public void With_WhenTargetInputTokensNegative_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest(0.5d);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { TargetInputTokens = -1 });

        exception.ParamName.ShouldBe(nameof(CompactionRequest.TargetInputTokens));
    }

    [Fact]
    public void With_WhenTargetInputTokensZero_AcceptsValue()
    {
        var request = CreateRequest(0.5d);

        var changed = request with { TargetInputTokens = 0 };

        changed.TargetInputTokens.ShouldBe(0);
    }

    [Fact]
    public void With_WhenDeadlineDoesNotFollowRequestedAt_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest(0.5d);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { Deadline = request.RequestedAt });

        exception.ParamName.ShouldBe(nameof(CompactionRequest.Deadline));
    }

    [Fact]
    public void With_WhenDeadlineFollowsRequestedAt_AcceptsValue()
    {
        var request = CreateRequest(0.5d);
        var later = request.RequestedAt.AddTicks(1);

        var changed = request with { Deadline = later };

        changed.Deadline.ShouldBe(later);
    }

    [Fact]
    public void With_WhenRequestedAtReachesDeadline_ThrowsArgumentOutOfRangeException()
    {
        var request = CreateRequest(0.5d);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { RequestedAt = request.Deadline });

        exception.ParamName.ShouldBe(nameof(CompactionRequest.RequestedAt));
    }

    [Fact]
    public void With_WhenDeadlineIsSetBeforeRequestedAtInOneExpression_AcceptsBothValues()
    {
        var request = CreateRequest(0.5d);
        var newRequestedAt = request.Deadline.AddHours(1);
        var newDeadline = newRequestedAt.AddMinutes(1);

        var changed = request with { Deadline = newDeadline, RequestedAt = newRequestedAt };

        changed.RequestedAt.ShouldBe(newRequestedAt);
        changed.Deadline.ShouldBe(newDeadline);
    }

    private static CompactionRequest CreateRequest(double minimumReductionRatio, DateTimeOffset? deadline = null) => new(
        TestSupport.TestSecurityEvidence.CompactionContext(
            new CompactionId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            new InRunOperationCorrelation(
                new OperationId(Guid.NewGuid()),
                new RunId(Guid.NewGuid()),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        new BranchId(Guid.NewGuid()),
        new SessionVersion(1),
        new SessionSequence(1),
        new ContextEpoch(1),
        new CompactionTrigger(CompactionTriggerKind.ExplicitMaintenance, "test", null),
        100,
        minimumReductionRatio,
        1,
        DateTimeOffset.UnixEpoch,
        deadline ?? DateTimeOffset.UnixEpoch.AddMinutes(1),
        ExtensionData.Empty);
}
