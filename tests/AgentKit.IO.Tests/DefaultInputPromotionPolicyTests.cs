// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Verifies deterministic bounded first-party promotion selection.</summary>
public sealed class DefaultInputPromotionPolicyTests
{
    [Fact]
    public async Task PlanAsync_WhenSteeringBoundary_SelectsOnlySteersInAdmissionOrder()
    {
        var policy = ResolvePolicy();
        var context = Context(
            Admitted(3, InputDelivery.Steer),
            Admitted(1, InputDelivery.FollowUp),
            Admitted(2, InputDelivery.Steer));

        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds
            .ShouldBe([Admission(2), Admission(3)]);
    }

    [Fact]
    public async Task PlanAsync_WhenIdle_SelectsOldestFollowUpThenEveryEligibleSteer()
    {
        var policy = ResolvePolicy();
        var context = Context(PromotionBoundary.OtherwiseIdle, 4,
            Admitted(4, InputDelivery.FollowUp), Admitted(2, InputDelivery.Steer),
            Admitted(1, InputDelivery.FollowUp), Admitted(3, InputDelivery.Steer));

        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds
            .ShouldBe([Admission(1), Admission(2), Admission(3)]);
    }

    [Fact]
    public async Task PlanAsync_WhenBoundCannotIncludeRequiredSteers_ReturnsTypedRejectionWithoutTruncating()
    {
        var policy = ResolvePolicy();
        var context = Context(PromotionBoundary.AfterTurnCommitted, 1,
            Admitted(1, InputDelivery.Steer), Admitted(2, InputDelivery.Steer));

        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        var rejection = result.ShouldBeOfType<InputPromotionPlanRejected>();
        rejection.Kind.ShouldBe(InputPromotionPlanRejectionKind.SelectionLimitExceeded);
        rejection.RequiredCount.ShouldBe(2);
    }

    [Fact]
    public async Task PlanAsync_WhenSteeringBoundaryHasManyIrrelevantFollowUps_SelectsBoundedSteerOnly()
    {
        var policy = ResolvePolicy();
        var inputs = Enumerable.Range(2, 10_000)
            .Select(index => Admitted(index, InputDelivery.FollowUp))
            .Prepend(Admitted(1, InputDelivery.Steer))
            .ToArray();
        var context = Context(PromotionBoundary.AfterTurnCommitted, 1, inputs);

        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds.ShouldBe([Admission(1)]);
    }

    [Fact]
    public async Task PlanAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var policy = ResolvePolicy();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), cancellation.Token).AsTask();

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AddInputPromotionPolicy_WhenCalledTwice_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();

        _ = services.AddInputPromotionPolicy().AddInputPromotionPolicy();

        services.Count(descriptor => descriptor.ServiceType == typeof(IInputPromotionPolicy)
            && descriptor.ImplementationType == typeof(DefaultInputPromotionPolicy)).ShouldBe(1);
    }

    [Fact]
    public void AddInputPromotionPolicy_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(services.AddInputPromotionPolicy);

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void RecordPromotionPlan_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        var boundaryException = Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordPromotionPlan((PromotionBoundary) 42, InputPromotionPlanOutcome.Planned, null));
        var outcomeException = Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordPromotionPlan(PromotionBoundary.OtherwiseIdle, (InputPromotionPlanOutcome) 42, null));
        var durationException = Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordPromotionPlan(PromotionBoundary.OtherwiseIdle, InputPromotionPlanOutcome.Planned, TimeSpan.FromTicks(-1)));

        boundaryException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        boundaryException.ParamName.ShouldBe("boundary");
        outcomeException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        outcomeException.ParamName.ShouldBe("outcome");
        durationException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        durationException.ParamName.ShouldBe("elapsed");
    }

    private static IInputPromotionPolicy ResolvePolicy() =>
        new ServiceCollection().AddInputPromotionPolicy().BuildServiceProvider().GetRequiredService<IInputPromotionPolicy>();

    internal static InputPromotionContext Context(params AdmittedInput[] inputs) =>
        Context(PromotionBoundary.AfterTurnCommitted, 16, inputs);

    internal static InputPromotionContext Context(PromotionBoundary boundary, int maximumPromotions, params AdmittedInput[] inputs) =>
        new(Agent(), Session(), new ExecutionLaneId("primary"), Operation(), new OperationStateRevision(2),
            new SessionBranchCursor(Branch(), Entry(9)), new SessionSequence(Math.Max(10, inputs.Max(static input => input.AdmittedSequence.Value))), new SessionVersion(4), null,
            boundary, Turn(), NextTurn(), [.. inputs], maximumPromotions);

    internal static AdmittedInput Admitted(long sequence, InputDelivery delivery)
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var input = new AgentInput(Input(sequence), delivery,
            [new TextPart($"input-{sequence}", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new AdmittedInput(Admission(sequence), Agent(), Session(), new ExecutionLaneId("primary"), identity,
            new SessionSequence(sequence), input, input,
            new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint($"original:{sequence}"), new InputFingerprint($"effective:{sequence}")),
            DateTimeOffset.UnixEpoch.AddSeconds(sequence));
    }

    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static SessionEntryId Entry(long value) => new(Guid.Parse($"20000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("60000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(
        new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
        new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001")), Turn());
}
