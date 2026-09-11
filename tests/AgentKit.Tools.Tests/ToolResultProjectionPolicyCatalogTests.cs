// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolResultProjectionPolicyCatalogTests: ToolResultProjectionPolicyCatalogConformanceTests
{
    protected override ToolResultProjectionPolicyCatalogFixture CreateFixture(ImmutableArray<ToolResultProjectionPolicySnapshot> policies) =>
        new RegisteredProjectionPolicyCatalogFixture(policies);

    [Fact]
    public void Constructor_WhenDependenciesAreNull_RejectsExactParameters()
    {
        // Arrange
        var logger = NullLogger<ToolResultProjectionPolicyCatalog>.Instance;

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyCatalog(null!, TimeProvider.System, logger)).ParamName.ShouldBe("policies");
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyCatalog([], null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyCatalog([], TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyCatalog([null!], TimeProvider.System, logger)).ParamName.ShouldBe("policies");
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("parts")]
    [InlineData("transformations")]
    [InlineData("extensions")]
    public void Constructor_WhenReferenceHasConflictingContent_RejectsComposition(string field)
    {
        // Arrange
        var original = ToolProjectionPolicyTestData.Snapshot();
        var changed = field switch
        {
            "bytes" => ToolProjectionPolicyTestData.Snapshot(maximumBytes: 2048),
            "parts" => ToolProjectionPolicyTestData.Snapshot(maximumParts: 8),
            "transformations" => ToolProjectionPolicyTestData.Snapshot(transformations: ToolResultProjectionTransformations.None),
            _ => ToolProjectionPolicyTestData.Snapshot(extensions: new ExtensionData(
                ImmutableDictionary<string, ExtensionValue>.Empty.Add("future", new ExtensionValue([.. "true"u8])))),
        };

        // Act
        var exception = Should.Throw<ArgumentException>(() => Catalog([original, changed]));

        // Assert
        exception.ParamName.ShouldBe("policies");
    }

    [Fact]
    public async Task Constructor_WhenEquivalentReferencesAreRepeated_AcceptsStructuralDuplicates()
    {
        // Arrange
        var original = ToolProjectionPolicyTestData.Snapshot(extensions: Extensions());
        var reconstructed = ToolProjectionPolicyTestData.Snapshot(extensions: Extensions());
        var catalog = Catalog([original, reconstructed]);

        // Act
        var result = await catalog.ResolveAsync(reconstructed.Reference, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task Constructor_WhenCallerChangesSourceCollection_CatalogRetainsCapturedRevisions()
    {
        // Arrange
        var retained = ToolProjectionPolicyTestData.Snapshot();
        var later = ToolProjectionPolicyTestData.Snapshot(version: 2);
        List<ToolResultProjectionPolicySnapshot> policies = [retained];
        var catalog = Catalog(policies);
        policies.Clear();
        policies.Add(later);

        // Act / Assert
        (await catalog.ResolveAsync(retained.Reference, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBeSameAs(retained);
        (await catalog.ResolveAsync(later.Reference, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(later.Reference);
    }

    private static ToolResultProjectionPolicyCatalog Catalog(IEnumerable<ToolResultProjectionPolicySnapshot> policies) =>
        new(policies, TimeProvider.System, NullLogger<ToolResultProjectionPolicyCatalog>.Instance);

    private static ExtensionData Extensions() => new(ImmutableDictionary<string, ExtensionValue>.Empty
        .Add("second", new ExtensionValue([.. "2"u8])).Add("first", new ExtensionValue([.. "1"u8])));

    [Theory]
    [InlineData("resolved", 4021, LogLevel.Debug)]
    [InlineData("unavailable", 4022, LogLevel.Warning)]
    [InlineData("cancelled", 4023, LogLevel.Information)]
    public async Task ResolveAsync_WhenObserved_RecordsSafeCorrelatedSignals(string outcome, int terminalEvent, LogLevel terminalLevel)
    {
        // Arrange
        const string protectedContent = "projection-extension-content-must-not-be-logged";
        var policy = ToolProjectionPolicyTestData.Snapshot(extensions: new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add("private", new ExtensionValue([.. System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(protectedContent))]))));
        var logger = new RecordingLogger<ToolResultProjectionPolicyCatalog>();
        long timestamp = 0;
        var clock = new CallbackTimestampTimeProvider(() => Interlocked.Add(ref timestamp, 125));
        var catalog = new ToolResultProjectionPolicyCatalog(outcome == "unavailable" ? [] : [policy], clock, logger);
        using var parent = new Activity("projection-policy-observation").Start();
        Activity? observed = null;
        using var activities = Listen(parent, activity => observed = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measurements.Add((name, value, tags)));
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled")
        {
            await cancellation.CancelAsync();
        }

        // Act
        if (outcome == "cancelled")
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await catalog.ResolveAsync(policy.Reference, cancellation.Token));
        }
        else
        {
            _ = await catalog.ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);
        }

        // Assert
        _ = observed.ShouldNotBeNull();
        observed.OperationName.ShouldBe(AgentKitActivityNames.ToolResultProjectionPolicyResolve);
        observed.ParentId.ShouldBe(parent.Id);
        observed.Status.ShouldBe(outcome == "resolved" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        observed.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        observed.GetTagItem(AgentKitTagNames.ToolResultProjectionPolicyKey).ShouldBe(policy.Reference.Key.Value);
        observed.GetTagItem(AgentKitTagNames.ToolResultProjectionPolicyVersion).ShouldBe(policy.Reference.Version.Value);
        if (outcome != "resolved")
        {
            observed.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome);
        }

        var entries = logger.Snapshot();
        entries.Select(static entry => entry.EventId.Id).ShouldBe([4020, terminalEvent]);
        entries[^1].Level.ShouldBe(terminalLevel);
        entries.ShouldAllBe(entry => entry.Category == typeof(ToolResultProjectionPolicyCatalog).FullName);
        foreach (var entry in entries)
        {
            entry.State["PolicyKey"].ShouldBe(policy.Reference.Key);
            entry.State["PolicyVersion"].ShouldBe(policy.Reference.Version);
            entry.Message.ShouldNotContain(protectedContent);
            entry.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(protectedContent, StringComparison.Ordinal));
        }

        observed.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(protectedContent, StringComparison.Ordinal));
        measurements.Count.ShouldBe(2);
        measurements.Single(item => item.Name == AgentKitMetricNames.ToolResultProjectionPolicyResolutionCount).Value.ShouldBe(1);
        measurements.Single(item => item.Name == AgentKitMetricNames.ToolResultProjectionPolicyResolutionDuration).Value.ShouldBe(0.125);
        foreach (var (Name, Value, Tags) in measurements)
        {
            Tags.ShouldBe([new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)]);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenReferenceIsInvalid_RejectsBeforeDiagnosticEffects()
    {
        // Arrange
        var logger = new RecordingLogger<ToolResultProjectionPolicyCatalog>();
        var clockCalls = 0;
        var catalog = new ToolResultProjectionPolicyCatalog([], new CallbackTimestampTimeProvider(() => ++clockCalls), logger);
        using var parent = new Activity("invalid-projection-policy").Start();
        var activities = 0;
        using var listener = Listen(parent, _ => activities++);

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await catalog.ResolveAsync(null!, TestContext.Current.CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("reference");
        clockCalls.ShouldBe(0);
        activities.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("resolved")]
    [InlineData("unavailable")]
    [InlineData("cancelled")]
    public async Task ResolveAsync_WhenLoggerThrows_PreservesTheSemanticOutcome(string outcome)
    {
        // Arrange
        var policy = ToolProjectionPolicyTestData.Snapshot();
        var logger = new RecordingLogger<ToolResultProjectionPolicyCatalog> { ThrowOnWrite = true };
        var catalog = new ToolResultProjectionPolicyCatalog(outcome == "unavailable" ? [] : [policy], TimeProvider.System, logger);
        using var cancellation = new CancellationTokenSource();

        // Act / Assert
        if (outcome == "cancelled")
        {
            await cancellation.CancelAsync();
            var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await catalog.ResolveAsync(policy.Reference, cancellation.Token));
            exception.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            var result = await catalog.ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);
            if (outcome == "resolved")
            {
                result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(policy);
            }
            else
            {
                result.ShouldBeOfType<ToolResultProjectionPolicyUnavailable>().Reference.ShouldBe(policy.Reference);
            }
        }
    }

    [Theory]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    public async Task ResolveAsync_WhenActivityListenerThrows_PreservesResolutionAndParent(string stage)
    {
        // Arrange
        var policy = ToolProjectionPolicyTestData.Snapshot();
        using var parent = new Activity("throwing-projection-listener").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) =>
            {
                return stage == "sample" && options.Name == AgentKitActivityNames.ToolResultProjectionPolicyResolve && options.Parent.TraceId == parent.TraceId
                    ? throw new InvalidOperationException("observer failure")
                    : ActivitySamplingResult.AllDataAndRecorded;
            },
            ActivityStarted = activity => Fail("start", activity),
            ActivityStopped = activity => Fail("stop", activity),
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var result = await Catalog([policy]).ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(policy);
        Activity.Current.ShouldBeSameAs(parent);
        return;

        void Fail(string callback, Activity activity)
        {
            if (stage == callback && activity.OperationName == AgentKitActivityNames.ToolResultProjectionPolicyResolve && activity.ParentId == parent.Id)
            {
                throw new InvalidOperationException("observer failure");
            }
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenMetricListenerThrows_PreservesResolution()
    {
        // Arrange
        var policy = ToolProjectionPolicyTestData.Snapshot();
        using var parent = new Activity("throwing-projection-metrics").Start();
        using var activities = Listen(parent, _ => { });
        var callbacks = 0;
        using var metrics = ListenMetrics(parent, (_, _, _) =>
        {
            callbacks++;
            throw new InvalidOperationException("observer failure");
        });

        // Act
        var result = await Catalog([policy]).ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(policy);
        callbacks.ShouldBe(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ResolveAsync_WhenClockFailsOrMovesBackward_OmitsUnmeasuredDuration(int failure)
    {
        // Arrange
        var policy = ToolProjectionPolicyTestData.Snapshot();
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() =>
        {
            reads++;
            return reads == failure ? throw new InvalidOperationException("clock failure") : failure == 3 ? -reads : reads;
        });
        var catalog = new ToolResultProjectionPolicyCatalog([policy], clock, NullLogger<ToolResultProjectionPolicyCatalog>.Instance);
        using var parent = new Activity("projection-clock-failure").Start();
        using var activities = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name));

        // Act
        var result = await catalog.ResolveAsync(policy.Reference, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeOfType<ToolResultProjectionPolicyResolved>().Snapshot.ShouldBe(policy);
        names.ShouldBe([AgentKitMetricNames.ToolResultProjectionPolicyResolutionCount]);
        reads.ShouldBe(failure == 1 ? 1 : 2);
    }

    private static ActivityListener Listen(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.ToolResultProjectionPolicyResolve && activity.ParentId == parent.Id)
                {
                    stopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenMetrics(Activity parent, Action<string, double, KeyValuePair<string, object?>[]> measured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is
                    AgentKitMetricNames.ToolResultProjectionPolicyResolutionCount or AgentKitMetricNames.ToolResultProjectionPolicyResolutionDuration)
                {
                    observer.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;

        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current is { } activity && activity.ParentId == parent.Id && activity.OperationName == AgentKitActivityNames.ToolResultProjectionPolicyResolve)
            {
                measured(instrument.Name, value, tags.ToArray());
            }
        }
    }
}
