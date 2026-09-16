// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class DefaultInputCoordinatorTests
{
    [Theory]
    [InlineData("queue")]
    [InlineData("admissionIds")]
    [InlineData("timeProvider")]
    [InlineData("options")]
    public void Constructor_WhenADependencyIsNull_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultInputCoordinator(
            parameter == "queue" ? null! : new RecordingInputQueue(),
            parameter == "admissionIds" ? null! : new GuidAdmissionIdGenerator(),
            parameter == "timeProvider" ? null! : TimeProvider.System,
            parameter == "options" ? null! : Options.Create(new InputCoordinatorOptions())));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenOptionsValueIsNull_RejectsTheOptionsArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultInputCoordinator(
            new RecordingInputQueue(),
            new GuidAdmissionIdGenerator(),
            TimeProvider.System,
            new NullValueOptions()));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenBoundPartBoundIsNotPositive_RejectsTheOptionsArgument(int parts)
    {
        var options = Options.Create(new InputCoordinatorOptions { MaximumInputParts = parts });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultInputCoordinator(
            new RecordingInputQueue(),
            new GuidAdmissionIdGenerator(),
            TimeProvider.System,
            options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenBoundPreprocessingRevisionIsDefault_RejectsTheOptionsArgument()
    {
        var options = Options.Create(new InputCoordinatorOptions { PreprocessingConfigurationVersion = default });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultInputCoordinator(
            new RecordingInputQueue(),
            new GuidAdmissionIdGenerator(),
            TimeProvider.System,
            options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenBoundPartBoundIsExactlyOne_AcceptsTheBoundary()
    {
        var options = Options.Create(new InputCoordinatorOptions { MaximumInputParts = 1 });

        var coordinator = new DefaultInputCoordinator(new RecordingInputQueue(), new GuidAdmissionIdGenerator(), TimeProvider.System, options);

        _ = coordinator.ShouldNotBeNull();
    }

    [Fact]
    public async Task AdmitAsync_WhenOptionsChangeAfterConstruction_KeepsTheValuesCapturedAtConstruction()
    {
        var queue = new RecordingInputQueue();
        var options = new InputCoordinatorOptions(maximumInputParts: 2);
        var coordinator = Coordinator(queue, options: options);
        options.MaximumInputParts = 1;

        var result = await coordinator.AdmitAsync(InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload(parts: 2)), TestContext.Current.CancellationToken);

        result.ShouldNotBeOfType<RejectedInput>();
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AdmitAsync_WhenRequestIsNull_RejectsExactArgument()
    {
        var coordinator = Coordinator(new RecordingInputQueue());

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await coordinator.AdmitAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task AdmitAsync_WhenPayloadIsAdmitted_HandsTheQueueCompleteDeterministicEvidence()
    {
        var queue = new RecordingInputQueue();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddMinutes(5));
        var options = new InputCoordinatorOptions(new ConfigurationVersion(7));
        var request = InputCoordinationTestData.AdmissionRequest();
        var coordinator = Coordinator(queue, clock: clock, options: options);

        var result = await coordinator.AdmitAsync(request, TestContext.Current.CancellationToken);

        queue.AppendCalls.ShouldBe(1);
        queue.AppendedRequest.ShouldBeSameAs(request);
        queue.AppendedAdmissionId.ShouldNotBe(default);
        queue.AppendedEffectiveInput.ShouldBeSameAs(request.Input);
        queue.AppendedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
        var expected = InputPayloadFingerprint.Create(request.Input);
        queue.AppendedPreprocessing!.ConfigurationVersion.ShouldBe(new ConfigurationVersion(7));
        queue.AppendedPreprocessing.OriginalFingerprint.ShouldBe(expected);
        queue.AppendedPreprocessing.EffectiveFingerprint.ShouldBe(expected);
        _ = result.ShouldBeOfType<AcceptedInput>();
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueReturnsANonAcceptingOutcome_ReturnsItUnchanged()
    {
        var conflict = new InputConflict(InputCoordinationTestData.Payload().Id, "different content");
        var queue = new RecordingInputQueue(admit: (_, _) => conflict);

        var result = await Coordinator(queue).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(conflict);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueReturnsCapacityExceeded_ReturnsItUnchanged()
    {
        var capacityExceeded = new QueueCapacityExceeded(new InputCapacityLimit(10, 10), TimeSpan.FromSeconds(1));
        var queue = new RecordingInputQueue(admit: (_, _) => capacityExceeded);

        var result = await Coordinator(queue).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(capacityExceeded);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueItselfRejects_ReturnsItUnchanged()
    {
        var rejected = new RejectedInput(new InputRejection(InputRejectionKind.InvalidInput, "queue rejection"));
        var queue = new RecordingInputQueue(admit: (_, _) => rejected);

        var result = await Coordinator(queue).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(rejected);
    }

    [Fact]
    public async Task AdmitAsync_WhenThePayloadExceedsThePartBound_RejectsBeforeTouchingTheQueue()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, options: new InputCoordinatorOptions(maximumInputParts: 2));

        var result = await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload(parts: 3)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.InvalidInput);
        queue.AppendCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AdmitAsync_WhenThePayloadMeetsThePartBound_StillAdmits()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, options: new InputCoordinatorOptions(maximumInputParts: 3));

        var result = await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload(parts: 3)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AcceptedInput>();
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheGeneratorProducesADefaultIdentity_FailsBeforeTouchingTheQueue()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, admissionIds: new FixedAdmissionIdGenerator(default));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken));

        queue.AppendCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AdmitAsync_WhenCancelledBeforeAdmission_DoesNotTouchTheQueue()
    {
        var queue = new RecordingInputQueue();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Coordinator(queue).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), cancellation.Token));

        queue.AppendCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AdmitAsync_WhenTheQueueFails_PropagatesWithoutSubstitutingAnOutcome()
    {
        var queue = new RecordingInputQueue(admit: (_, _) => throw new InvalidTimeZoneException("queue failure"));

        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () => await Coordinator(queue).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdmitAsync_WhenTheClockAndLoggerFail_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(
            queue,
            clock: new ThrowingTimeProvider(),
            logger: new ThrowingLogger<DefaultInputCoordinator>());

        var result = await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AcceptedInput>();
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AdmitAsync_WhenMetricsRecordingFailsFromNegativeElapsedTime_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, clock: new NegativeElapsedTimeProvider());

        var result = await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AcceptedInput>();
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AdmitAsync_WhenElapsedTimeMeasurementFails_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, clock: new ElapsedThrowingTimeProvider());

        var result = await coordinator.AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AcceptedInput>();
        queue.AppendCalls.ShouldBe(1);
    }

    [Fact]
    public async Task AdmitAsync_WhenSeparateRequestsCarryEqualPayloads_ProducesTheSameFingerprint()
    {
        var first = new RecordingInputQueue();
        var second = new RecordingInputQueue();
        var different = new RecordingInputQueue();

        _ = await Coordinator(first).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload("same")),
            TestContext.Current.CancellationToken);
        _ = await Coordinator(second).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload("same")),
            TestContext.Current.CancellationToken);
        _ = await Coordinator(different).AdmitAsync(
            InputCoordinationTestData.AdmissionRequest(InputCoordinationTestData.Payload("other")),
            TestContext.Current.CancellationToken);

        second.AppendedPreprocessing!.OriginalFingerprint.ShouldBe(first.AppendedPreprocessing!.OriginalFingerprint);
        different.AppendedPreprocessing!.OriginalFingerprint.ShouldNotBe(first.AppendedPreprocessing.OriginalFingerprint);
    }

    [Fact]
    public async Task AdmitAsync_WhenRepeatedForTheSamePayload_AllocatesADistinctAttemptIdentity()
    {
        var first = new RecordingInputQueue();
        var second = new RecordingInputQueue();
        var request = InputCoordinationTestData.AdmissionRequest();

        _ = await Coordinator(first).AdmitAsync(request, TestContext.Current.CancellationToken);
        _ = await Coordinator(second).AdmitAsync(request, TestContext.Current.CancellationToken);

        second.AppendedAdmissionId.ShouldNotBe(first.AppendedAdmissionId);
    }

    [Fact]
    public async Task PromoteAsync_WhenRequestIsNull_RejectsExactArgument()
    {
        var coordinator = Coordinator(new RecordingInputQueue());

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await coordinator.PromoteAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task PromoteAsync_WhenTheQueueCommits_ForwardsTheExactRequestAndReturnsItsOutcome()
    {
        var conflict = new InputPromotionConflict(InputPromotionConflictKind.OperationRevisionChanged, "stale");
        var queue = new RecordingInputQueue(promote: _ => conflict);
        var request = InputCoordinationTestData.PromotionRequest();

        var result = await Coordinator(queue).PromoteAsync(request, TestContext.Current.CancellationToken);

        queue.PromoteCalls.ShouldBe(1);
        queue.PromotedRequest.ShouldBeSameAs(request);
        result.ShouldBeSameAs(conflict);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheQueueCommitsSuccessfully_ReturnsItUnchanged()
    {
        var promoted = InputCoordinationTestData.Promoted();
        var queue = new RecordingInputQueue(promote: _ => promoted);

        var result = await Coordinator(queue).PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(promoted);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheQueueRejects_ReturnsItUnchanged()
    {
        var rejected = InputCoordinationTestData.Rejected();
        var queue = new RecordingInputQueue(promote: _ => rejected);

        var result = await Coordinator(queue).PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(rejected);
    }

    [Fact]
    public async Task PromoteAsync_WhenCancelledBeforePromotion_DoesNotTouchTheQueue()
    {
        var queue = new RecordingInputQueue();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Coordinator(queue).PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), cancellation.Token));

        queue.PromoteCalls.ShouldBe(0);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheLoggerFails_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, logger: new ThrowingLogger<DefaultInputCoordinator>());

        var result = await coordinator.PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionRejected>();
        queue.PromoteCalls.ShouldBe(1);
    }

    [Fact]
    public async Task PromoteAsync_WhenMetricsRecordingFailsFromNegativeElapsedTime_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, clock: new NegativeElapsedTimeProvider());

        var result = await coordinator.PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionRejected>();
        queue.PromoteCalls.ShouldBe(1);
    }

    [Fact]
    public async Task PromoteAsync_WhenElapsedTimeMeasurementFails_StillReturnsTheQueueOutcome()
    {
        var queue = new RecordingInputQueue();
        var coordinator = Coordinator(queue, clock: new ElapsedThrowingTimeProvider());

        var result = await coordinator.PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionRejected>();
        queue.PromoteCalls.ShouldBe(1);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheQueueFails_PropagatesWithoutSubstitutingAnOutcome()
    {
        var queue = new RecordingInputQueue(promote: _ => throw new InvalidTimeZoneException("queue failure"));

        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () => await Coordinator(queue).PromoteAsync(
            InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RecordInputAdmission_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordInputAdmission(Enum.Parse<InputAdmissionOutcome>("42"), null))
            .ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordInputAdmission(InputAdmissionOutcome.Accepted, TimeSpan.FromTicks(-1)))
            .ParamName.ShouldBe("elapsed");
    }

    [Fact]
    public void RecordInputPromotion_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordInputPromotion(Enum.Parse<PromotionBoundary>("42"), InputPromotionOutcome.Promoted, null))
            .ParamName.ShouldBe("boundary");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordInputPromotion(PromotionBoundary.OtherwiseIdle, Enum.Parse<InputPromotionOutcome>("42"), null))
            .ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            IOMetrics.RecordInputPromotion(PromotionBoundary.OtherwiseIdle, InputPromotionOutcome.Promoted, TimeSpan.FromTicks(-1)))
            .ParamName.ShouldBe("elapsed");
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_RejectsExactArgument()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Enum.Parse<InputAdmissionOutcome>("42").ToStableValue());
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Enum.Parse<InputPromotionOutcome>("42").ToStableValue());
    }

    [Fact]
    public void ToStableValue_WhenAdmissionOutcomeIsDefined_ReturnsOneDistinctStableValueEach()
    {
        InputAdmissionOutcome.Accepted.ToStableValue().ShouldBe("accepted");
        InputAdmissionOutcome.Conflict.ToStableValue().ShouldBe("conflict");
        InputAdmissionOutcome.CapacityExceeded.ToStableValue().ShouldBe("capacity_exceeded");
        InputAdmissionOutcome.Rejected.ToStableValue().ShouldBe("rejected");
        InputAdmissionOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
        InputAdmissionOutcome.Failed.ToStableValue().ShouldBe("failed");
    }

    [Fact]
    public void ToStableValue_WhenPromotionOutcomeIsDefined_ReturnsOneDistinctStableValueEach()
    {
        InputPromotionOutcome.Promoted.ToStableValue().ShouldBe("promoted");
        InputPromotionOutcome.Conflict.ToStableValue().ShouldBe("conflict");
        InputPromotionOutcome.Rejected.ToStableValue().ShouldBe("rejected");
        InputPromotionOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
        InputPromotionOutcome.Failed.ToStableValue().ShouldBe("failed");
    }

    private static DefaultInputCoordinator Coordinator(
        RecordingInputQueue queue,
        IIdentifierGenerator<AdmissionId>? admissionIds = null,
        TimeProvider? clock = null,
        InputCoordinatorOptions? options = null,
        ILogger<DefaultInputCoordinator>? logger = null) =>
        new(queue,
            admissionIds ?? new GuidAdmissionIdGenerator(),
            clock ?? TimeProvider.System,
            Options.Create(options ?? new InputCoordinatorOptions()),
            logger);

    private sealed class NullValueOptions: IOptions<InputCoordinatorOptions>
    {
        public InputCoordinatorOptions Value => null!;
    }

    private sealed class FixedAdmissionIdGenerator: IIdentifierGenerator<AdmissionId>
    {
        private readonly AdmissionId _value;

        internal FixedAdmissionIdGenerator(AdmissionId value) => _value = value;

        public AdmissionId Create() => _value;
    }

    private sealed class FakeTimeProvider: TimeProvider
    {
        private readonly DateTimeOffset _now;

        internal FakeTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ThrowingTimeProvider: TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

        public override long GetTimestamp() => throw new InvalidTimeZoneException("clock failure");
    }

    /// <summary>Yields a decreasing timestamp sequence so the base <c>GetElapsedTime(long)</c> implementation computes a negative duration.</summary>
    private sealed class NegativeElapsedTimeProvider: TimeProvider
    {
        private long _next = 1_000_000L;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

        public override long GetTimestamp() => Interlocked.Add(ref _next, -100_000L);
    }

    /// <summary>Succeeds on the timestamp captured at the start of an operation but fails when elapsed time is later measured.</summary>
    private sealed class ElapsedThrowingTimeProvider: TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

        public override long GetTimestamp() =>
            Interlocked.Increment(ref _calls) == 1 ? base.GetTimestamp() : throw new InvalidTimeZoneException("clock failure");
    }

    private sealed class ThrowingLogger<TCategory>: ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidTimeZoneException("logger failure");
    }
}
