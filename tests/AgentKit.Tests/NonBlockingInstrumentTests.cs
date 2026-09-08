// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Diagnostics.Metrics;

public sealed class NonBlockingInstrumentTests
{
    [Fact]
    public void GetOrCreate_WhenFactoryIsNull_ThrowsExactArgumentNullException()
    {
        var cache = new NonBlockingInstrument<object>();

        var exception = Should.Throw<ArgumentNullException>(() => cache.GetOrCreate(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("create");
    }

    [Fact]
    public void GetOrCreate_WhenInstrumentPublishedReentersProviderBuild_DoesNotDeadlockOrLoseProvider()
    {
        const string meterName = "AgentKit.Tests.NonBlockingInstrument.Reentrant";
        const string instrumentName = "test.reentrant";
        using var meter = new Meter(meterName);
        var cache = new NonBlockingInstrument<Counter<long>>();
        Counter<long>? reentrantResult = null;
        ServiceProvider? reentrantProvider = null;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name != meterName || instrument.Name != instrumentName)
                {
                    return;
                }

                var providerFactory = new AgentKitServiceProviderFactory();
                reentrantProvider = (ServiceProvider) providerFactory.CreateServiceProvider(
                    new ServiceCollection());
                reentrantResult = cache.GetOrCreate(
                    () => throw new InvalidOperationException("A reentrant publisher must not run."));
            },
        };
        listener.Start();

        var result = cache.GetOrCreate(() => meter.CreateCounter<long>(instrumentName));

        _ = result.ShouldNotBeNull();
        reentrantResult.ShouldBeNull();
        reentrantProvider.ShouldNotBeNull().Dispose();
    }

    [Fact]
    public async Task GetOrCreate_WhenCreationIsConcurrent_PublishesOneWinnerWithoutWaiting()
    {
        var cache = new NonBlockingInstrument<object>();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var expected = new object();
        var competingExpected = new object();
        var competingFactoryCalls = 0;
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = Task.Run(
            () => cache.GetOrCreate(
                () =>
                {
                    entered.Set();
                    release.Wait(cancellationToken);
                    return expected;
                }),
            cancellationToken);
        entered.Wait(cancellationToken);

        try
        {
            var competing = cache.GetOrCreate(
                () =>
                {
                    competingFactoryCalls++;
                    return competingExpected;
                });

            competing.ShouldBeSameAs(competingExpected);
            competingFactoryCalls.ShouldBe(1);
        }
        finally
        {
            release.Set();
        }

        (await first).ShouldBeSameAs(expected);
        cache.GetOrCreate(static () => new object()).ShouldBeSameAs(competingExpected);
    }
}
