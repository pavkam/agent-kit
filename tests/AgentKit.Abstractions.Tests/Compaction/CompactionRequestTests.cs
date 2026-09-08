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

    private static CompactionRequest CreateRequest(double minimumReductionRatio) => new(
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
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        ExtensionData.Empty);
}
