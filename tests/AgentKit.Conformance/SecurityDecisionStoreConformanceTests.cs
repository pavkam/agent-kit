// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable behavioral cases for implementations of <see cref="ISecurityDecisionStore"/>.</summary>
/// <typeparam name="TFixture">The implementation fixture that composes a store.</typeparam>
public abstract class SecurityDecisionStoreConformanceTests<TFixture>
    where TFixture : ISecurityDecisionStoreConformanceFixture
{
    /// <summary>Creates a fresh fixture for one contract case.</summary>
    /// <returns>The fixture used by one conformance case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies null decisions are rejected before any append occurs.</summary>
    [Fact]
    public async Task RecordAsync_WhenDecisionIsNull_ThrowsBeforeRecording()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await store.RecordAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("decision");
    }

    /// <summary>Verifies decisions are retained in arrival order.</summary>
    [Fact]
    public async Task RecordAsync_WhenCalled_AppendsInOrder()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = Denied("security.no_policy");
        var second = Denied("security.approval_denied");

        await store.RecordAsync(first, TestContext.Current.CancellationToken);
        await store.RecordAsync(second, TestContext.Current.CancellationToken);

        var recorded = fixture.ReadRecorded(store);
        recorded.Count.ShouldBe(2);
        recorded[0].ShouldBe(first);
        recorded[1].ShouldBe(second);
    }

    /// <summary>Verifies cancellation before commit leaves the store unchanged.</summary>
    [Fact]
    public async Task RecordAsync_WhenCancelledBeforeCommit_DoesNotAppend()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var decision = Denied("security.cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.RecordAsync(decision, cancellation.Token));

        fixture.ReadRecorded(store).ShouldBeEmpty();
    }

    /// <summary>Verifies acknowledged decisions survive disposal and recreation when durability is supported.</summary>
    [Fact]
    public async Task RecordAsync_WhenStoreIsReopened_RetainsPriorDecisions()
    {
        await using var fixture = CreateFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var decision = Denied("security.durable");
        await store.RecordAsync(decision, TestContext.Current.CancellationToken);

        var reopened = await fixture.RecreateAsync(TestContext.Current.CancellationToken);
        var recorded = fixture.ReadRecorded(reopened);

        recorded.Count.ShouldBe(1);
        recorded[0].ShouldBe(decision);
    }

    private static SecurityDenied Denied(string code) =>
        new(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new SecurityPolicyVersion(1),
            new SecurityDenial(code, "Denied."));
}
