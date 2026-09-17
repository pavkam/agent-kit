// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies SequencingModelResponseObserver behavior and contracts.</summary>
public sealed class SequencingModelResponseObserverTests
{
    private static readonly ModelRequestId _requestId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    [Fact]
    public void Constructor_WhenInnerIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SequencingModelResponseObserver(null!)).ParamName.ShouldBe("inner");

    [Fact]
    public async Task OnEventAsync_WhenResponseEventIsNull_ThrowsExactArgumentNullException()
    {
        var observer = new SequencingModelResponseObserver(new FakeObserver());
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => observer.OnEventAsync(null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("responseEvent");
    }

    [Fact]
    public async Task OnEventAsync_WhenFullSequenceDelivered_RenumbersAndTracksState()
    {
        var inner = new FakeObserver();
        var observer = new SequencingModelResponseObserver(inner);
        var token = TestContext.Current.CancellationToken;
        var part = new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty);
        var usage = new ModelUsage(ModelUsageReportState.Final, 1, 2, null, null, null, null, ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseStarted(_requestId, 41), token);
        await observer.OnEventAsync(new ModelResponseStarted(_requestId, 42), token);
        await observer.OnEventAsync(new ModelPartCompleted(_requestId, 43, 0, part), token);
        await observer.OnEventAsync(new ModelUsageUpdated(_requestId, 44, usage), token);
        var cancellation = new ProviderFailure(ProviderFailureKind.Cancellation, new ProviderId("openai"), null, null, null, null, "cancelled", null, ExtensionData.Empty);
        await observer.OnEventAsync(new ModelResponseCancelled(_requestId, 45, cancellation, [], usage), token);
        await observer.OnEventAsync(new ModelResponseStarted(_requestId, 46), token);

        inner.Delivered.Count.ShouldBe(4);
        inner.Delivered[0].Sequence.ShouldBe(0);
        inner.Delivered[1].Sequence.ShouldBe(1);
        inner.Delivered[2].Sequence.ShouldBe(2);
        inner.Delivered[3].Sequence.ShouldBe(3);
        observer.NextSequence.ShouldBe(4);
        observer.HasStarted.ShouldBeTrue();
        observer.HasTerminated.ShouldBeTrue();
        observer.CompletedParts.ShouldBe([part]);
        observer.Usage.ShouldBe(usage);
    }

    [Fact]
    public async Task OnEventAsync_WhenInnerThrowsOnTheFirstStart_LeavesHasStartedFalseSoARetriedStartIsDelivered()
    {
        var inner = new FakeObserver { ThrowOnNextDelivery = true };
        var observer = new SequencingModelResponseObserver(inner);
        var token = TestContext.Current.CancellationToken;

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => observer.OnEventAsync(new ModelResponseStarted(_requestId, 1), token).AsTask());

        observer.HasStarted.ShouldBeFalse();
        observer.NextSequence.ShouldBe(0);

        await observer.OnEventAsync(new ModelResponseStarted(_requestId, 2), token);

        observer.HasStarted.ShouldBeTrue();
        inner.Delivered.ShouldHaveSingleItem().Sequence.ShouldBe(0);
    }

    private sealed class FakeObserver: IModelResponseObserver
    {
        public List<ModelResponseEvent> Delivered { get; } = [];

        public bool ThrowOnNextDelivery { get; set; }

        public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextDelivery)
            {
                ThrowOnNextDelivery = false;
                throw new InvalidOperationException("Simulated inner observer failure.");
            }

            Delivered.Add(responseEvent);
            return ValueTask.CompletedTask;
        }
    }
}
