// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class DefaultEvaluationResultStoreSelectorTests
{
    [Fact]
    public void Constructor_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DefaultEvaluationResultStoreSelector(null!)).ParamName.ShouldBe("services");

    [Fact]
    public async Task SelectAsync_WhenKeyIsBlank_ThrowsArgumentException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await new DefaultEvaluationResultStoreSelector(provider).SelectAsync(default, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public async Task SelectAsync_WhenTokenIsCancelled_ThrowsBeforeResolvingAnything()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await new DefaultEvaluationResultStoreSelector(provider).SelectAsync(new EvaluationResultStoreKey("s"), cancellation.Token));
    }

    [Fact]
    public async Task SelectAsync_WhenStoreIsRegisteredUnderTheKey_SelectsExactlyThatStore()
    {
        var store = new RecordingResultStore();
        var other = new RecordingResultStore();
        using var provider = new ServiceCollection()
            .AddKeyedSingleton<IEvaluationResultStore>("other", other)
            .AddKeyedSingleton<IEvaluationResultStore>("wanted", store)
            .BuildServiceProvider();

        var selection = await new DefaultEvaluationResultStoreSelector(provider).SelectAsync(new EvaluationResultStoreKey("wanted"), TestContext.Current.CancellationToken);

        var selected = selection.ShouldBeOfType<EvaluationResultStoreSelected>();
        selected.Store.ShouldBeSameAs(store);
        selected.Key.ShouldBe(new EvaluationResultStoreKey("wanted"));
    }

    [Fact]
    public async Task SelectAsync_WhenNoStoreIsRegisteredUnderTheKey_ReturnsUnavailableWithoutFallingBack()
    {
        using var provider = new ServiceCollection().AddKeyedSingleton<IEvaluationResultStore>("other", new RecordingResultStore()).BuildServiceProvider();

        var selection = await new DefaultEvaluationResultStoreSelector(provider).SelectAsync(new EvaluationResultStoreKey("missing"), TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<EvaluationResultStoreUnavailable>().Key.ShouldBe(new EvaluationResultStoreKey("missing"));
    }
}
