// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines the cross-store admission and promotion contract for <see cref="IInputQueue"/>.</summary>
/// <typeparam name="TFixture">The store fixture that composes one real session-backed queue.</typeparam>
/// <remarks>
/// Cases observe only the queue and the fixture's request builders. Equivalent replay is a newly allocated admission
/// identity for the same caller input, matching <see cref="IInputQueue.AppendAsync"/>'s admission-identity parameter.
/// The caller-visible idempotency identity is <see cref="AgentInput.Id"/>.
/// </remarks>
public abstract class InputQueueConformanceTests<TFixture>
    where TFixture : IInputQueueConformanceFixture
{
    /// <summary>Creates an isolated store-backed queue fixture.</summary>
    /// <returns>A fresh disposable fixture.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies an equivalent retry returns the original receipt and does not allocate another sequence.</summary>
    [Fact]
    public async Task AppendAsync_WhenIdenticalAdmissionIsRetried_ReturnsSameReceiptWithoutConsumingSequence()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var input = Payload("same-input");
        var request = await fixture.CreateAdmissionRequestAsync(input, InputQueueConformanceLane.Primary, cancellationToken);
        var first = await AdmitAsync(queue, request, new AdmissionId(Guid.NewGuid()), cancellationToken);
        var replay = await AdmitAsync(queue, request, new AdmissionId(Guid.NewGuid()), cancellationToken);
        var nextInput = Payload("next-input");
        var nextRequest = await fixture.CreateAdmissionRequestAsync(
            nextInput, InputQueueConformanceLane.Primary, cancellationToken);
        var next = await AdmitAsync(queue, nextRequest, new AdmissionId(Guid.NewGuid()), cancellationToken);

        replay.Receipt.AdmissionId.ShouldBe(first.Receipt.AdmissionId);
        replay.Receipt.InputId.ShouldBe(first.Receipt.InputId);
        replay.Receipt.AdmittedSequence.ShouldBe(first.Receipt.AdmittedSequence);
        replay.Receipt.ExecutionLaneId.ShouldBe(first.Receipt.ExecutionLaneId);
        replay.Receipt.Existing.ShouldBeTrue();
        first.Receipt.Existing.ShouldBeFalse();
        next.Receipt.AdmittedSequence.Value.ShouldBe(first.Receipt.AdmittedSequence.Value + 1);
    }

    /// <summary>Verifies reuse of one admission idempotency key with different content leaves the original in place.</summary>
    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyIsReusedWithDifferentContent_FailsWithoutReplacingOriginal()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var inputId = new InputId(Guid.NewGuid());
        var original = Payload(inputId, "keep-original");
        var conflicting = Payload(inputId, "replace-original");
        var request = await fixture.CreateAdmissionRequestAsync(original, InputQueueConformanceLane.Primary, cancellationToken);
        var conflictingRequest = await fixture.CreateAdmissionRequestAsync(
            conflicting, InputQueueConformanceLane.Primary, cancellationToken);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var admitted = await AdmitAsync(queue, request, admissionId, cancellationToken);

        var conflict = await queue.AppendAsync(
            conflictingRequest, admissionId, conflicting, Manifest(conflicting), DateTimeOffset.UnixEpoch,
            cancellationToken);

        var rejected = conflict.ShouldBeOfType<InputConflict>();
        rejected.InputId.ShouldBe(inputId);
        var promotion = await fixture.CreatePromotionRequestAsync(
            admitted.Receipt.AdmittedSequence, maximumPromotions: 8, InputQueueConformanceLane.Primary,
            cancellationToken);
        var promoted = (await queue.PromoteAsync(promotion, cancellationToken)).ShouldBeOfType<InputPromoted>();
        promoted.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([admitted.Receipt.AdmissionId]);
        Text(promoted.Promoted.Single()).ShouldBe("keep-original");
    }

    /// <summary>Verifies a captured cutoff promotes earlier admissions and leaves a later one pending for the next boundary.</summary>
    [Fact]
    public async Task PromoteAsync_WhenCutoffExcludesLaterAdmissions_OmitsItemsAdmittedAfterCutoff()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var early = await AdmitNewAsync(fixture, queue, "before-cutoff", InputQueueConformanceLane.Primary, cancellationToken);
        var late = await AdmitNewAsync(fixture, queue, "after-cutoff", InputQueueConformanceLane.Primary, cancellationToken);
        late.Receipt.AdmittedSequence.Value.ShouldBeGreaterThan(early.Receipt.AdmittedSequence.Value);
        var cutoff = await fixture.CreatePromotionRequestAsync(
            early.Receipt.AdmittedSequence, maximumPromotions: 8, InputQueueConformanceLane.Primary, cancellationToken);

        var promoted = (await queue.PromoteAsync(cutoff, cancellationToken)).ShouldBeOfType<InputPromoted>();

        promoted.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([early.Receipt.AdmissionId]);
        var remainder = await fixture.CreatePromotionRequestAsync(
            late.Receipt.AdmittedSequence, maximumPromotions: 8, InputQueueConformanceLane.Primary, cancellationToken);
        var later = (await queue.PromoteAsync(remainder, cancellationToken)).ShouldBeOfType<InputPromoted>();
        later.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([late.Receipt.AdmissionId]);
    }

    /// <summary>Verifies promotion consumes only the addressed lane and leaves the other lane's admission pending.</summary>
    [Fact]
    public async Task PromoteAsync_WhenAnotherLaneHasAdmittedInput_DoesNotPromoteThatItem()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var primary = await AdmitNewAsync(fixture, queue, "primary-lane", InputQueueConformanceLane.Primary, cancellationToken);
        var secondary = await AdmitNewAsync(
            fixture, queue, "secondary-lane", InputQueueConformanceLane.Secondary, cancellationToken);
        var cutoff = new SessionSequence(long.MaxValue);
        var primaryRequest = await fixture.CreatePromotionRequestAsync(
            cutoff, maximumPromotions: 8, InputQueueConformanceLane.Primary, cancellationToken);

        var promoted = (await queue.PromoteAsync(primaryRequest, cancellationToken)).ShouldBeOfType<InputPromoted>();

        promoted.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([primary.Receipt.AdmissionId]);
        promoted.Promoted.ShouldAllBe(item => item.ExecutionLaneId == primary.Receipt.ExecutionLaneId);
        var secondaryRequest = await fixture.CreatePromotionRequestAsync(
            cutoff, maximumPromotions: 8, InputQueueConformanceLane.Secondary, cancellationToken);
        var otherLane = (await queue.PromoteAsync(secondaryRequest, cancellationToken)).ShouldBeOfType<InputPromoted>();
        otherLane.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([secondary.Receipt.AdmissionId]);
    }

    /// <summary>Verifies a full queue returns typed backpressure, keeps older items, and still replays them without new capacity.</summary>
    [Fact]
    public async Task AppendAsync_WhenPendingCapacityIsExhausted_ReturnsQueueCapacityExceededWithoutDroppingOlderItems()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var admitted = new List<AcceptedInput>(fixture.MaximumPendingInputsPerLane);
        InputAdmissionRequest? firstRequest = null;
        for (var index = 0; index < fixture.MaximumPendingInputsPerLane; index++)
        {
            var input = Payload($"kept-{index}");
            var request = await fixture.CreateAdmissionRequestAsync(input, InputQueueConformanceLane.Primary, cancellationToken);
            if (index == 0)
            {
                firstRequest = request;
            }

            admitted.Add(await AdmitAsync(queue, request, new AdmissionId(Guid.NewGuid()), cancellationToken));
        }

        var overflow = Payload("overflow");
        var overflowRequest = await fixture.CreateAdmissionRequestAsync(
            overflow, InputQueueConformanceLane.Primary, cancellationToken);
        var rejected = await queue.AppendAsync(
            overflowRequest, new AdmissionId(Guid.NewGuid()), overflow, Manifest(overflow), DateTimeOffset.UnixEpoch,
            cancellationToken);
        var replay = await AdmitAsync(
            queue, firstRequest.ShouldNotBeNull(), new AdmissionId(Guid.NewGuid()), cancellationToken);

        var capacity = rejected.ShouldBeOfType<QueueCapacityExceeded>();
        capacity.Limit.MaximumPendingInputs.ShouldBe(fixture.MaximumPendingInputsPerLane);
        capacity.Limit.CurrentPendingInputs.ShouldBe(fixture.MaximumPendingInputsPerLane);
        replay.Receipt.AdmissionId.ShouldBe(admitted[0].Receipt.AdmissionId);
        replay.Receipt.AdmittedSequence.ShouldBe(admitted[0].Receipt.AdmittedSequence);
        replay.Receipt.Existing.ShouldBeTrue();
        var promotion = await fixture.CreatePromotionRequestAsync(
            new SessionSequence(long.MaxValue), maximumPromotions: fixture.MaximumPendingInputsPerLane,
            InputQueueConformanceLane.Primary, cancellationToken);
        var promoted = (await queue.PromoteAsync(promotion, cancellationToken)).ShouldBeOfType<InputPromoted>();
        promoted.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([.. admitted.Select(static item => item.Receipt.AdmissionId)]);
        promoted.Promoted.Select(Text).ShouldNotContain("overflow");
    }

    /// <summary>Verifies a second promotion of the same cutoff does not append the selected input again.</summary>
    [Fact]
    public async Task PromoteAsync_WhenEquivalentRequestIsRepeated_DoesNotReappendHistory()
    {
        await using var fixture = CreateFixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = await fixture.GetQueueAsync(cancellationToken);
        var admitted = await AdmitNewAsync(fixture, queue, "promote-once", InputQueueConformanceLane.Primary, cancellationToken);
        var request = await fixture.CreatePromotionRequestAsync(
            admitted.Receipt.AdmittedSequence, maximumPromotions: 1, InputQueueConformanceLane.Primary,
            cancellationToken);

        var first = (await queue.PromoteAsync(request, cancellationToken)).ShouldBeOfType<InputPromoted>();
        var second = await queue.PromoteAsync(request, cancellationToken);

        first.Promoted.Select(static item => item.AdmissionId).ToImmutableArray()
            .ShouldBe([admitted.Receipt.AdmissionId]);
        _ = first.Promoted.Single().PromotedSequence.ShouldNotBeNull();
        var rejected = second.ShouldBeOfType<InputPromotionRejected>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.NoEligibleInput);
    }

    private static async Task<AcceptedInput> AdmitNewAsync(
        TFixture fixture,
        IInputQueue queue,
        string text,
        InputQueueConformanceLane lane,
        CancellationToken cancellationToken)
    {
        var input = Payload(text);
        var request = await fixture.CreateAdmissionRequestAsync(input, lane, cancellationToken);
        return await AdmitAsync(queue, request, new AdmissionId(Guid.NewGuid()), cancellationToken);
    }

    private static async Task<AcceptedInput> AdmitAsync(
        IInputQueue queue,
        InputAdmissionRequest request,
        AdmissionId admissionId,
        CancellationToken cancellationToken)
    {
        var result = await queue.AppendAsync(
            request, admissionId, request.Input, Manifest(request.Input), DateTimeOffset.UnixEpoch, cancellationToken);
        return result.ShouldBeOfType<AcceptedInput>();
    }

    private static AgentInput Payload(string text) => Payload(new InputId(Guid.NewGuid()), text);

    private static AgentInput Payload(InputId inputId, string text) => new(
        inputId, InputDelivery.Steer, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

    private static InputPreprocessingManifest Manifest(AgentInput input)
    {
        var fingerprint = InputPayloadFingerprint.Create(input);
        return new InputPreprocessingManifest(new ConfigurationVersion(1), fingerprint, fingerprint);
    }

    private static string Text(AdmittedInput input) =>
        input.EffectivePayload.Parts.Single().ShouldBeOfType<TextPart>().Text;
}
