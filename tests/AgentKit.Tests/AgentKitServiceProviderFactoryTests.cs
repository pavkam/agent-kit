// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;

using AgentKit.Observability;

using Microsoft.Extensions.Logging;

public sealed class AgentKitServiceProviderFactoryTests
{
    private static readonly AsyncLocal<ActivityTraceId?> _activeMetricTrace = new();

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentKitServiceProviderFactory(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenInjectedLoggerOverloadOptionsIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AgentKitServiceProviderFactory(null!, new RecordingLogger()));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsExactArgumentNullExceptionBeforeCapturingOptions()
    {
        var options = new ServiceProviderOptions();

        var exception = Should.Throw<ArgumentNullException>(() =>
            new AgentKitServiceProviderFactory(options, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AgentKitServiceProviderFactory(
                new ServiceProviderOptions(), new RecordingLogger(), null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void CreateBuilder_WhenServicesIsNull_ThrowsExactArgumentNullException()
    {
        var factory = new AgentKitServiceProviderFactory();

        var exception = Should.Throw<ArgumentNullException>(() => factory.CreateBuilder(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void CreateBuilder_WhenServicesArePresent_ReturnsSameCollectionForHostConfiguration()
    {
        var services = new ServiceCollection();
        var factory = new AgentKitServiceProviderFactory();

        var builder = factory.CreateBuilder(services);

        builder.ShouldBeSameAs(services);
    }

    [Fact]
    public void CreateServiceProvider_WhenBuilderIsNull_ThrowsExactArgumentNullException()
    {
        var factory = new AgentKitServiceProviderFactory();

        var exception = Should.Throw<ArgumentNullException>(() => factory.CreateServiceProvider(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("containerBuilder");
    }

    [Fact]
    public void CreateServiceProvider_WhenBuilderContainsNull_RejectsBeforeDiagnosticsOrBuildEffects()
    {
        var applicationFactoryCalls = 0;
        var services = new NullContainingServiceCollection
        {
            ServiceDescriptor.Singleton<ILeaf>(
                _ =>
                {
                    applicationFactoryCalls++;
                    return new Leaf();
                }),
            null!,
        };
        var logger = new RecordingLogger();
        var observedActivities = 0;
        using var parent = new Activity("null-descriptor-parent").Start();
        using var listener = BuildActivityListener(
            started: activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.ParentSpanId == parent.SpanId)
                {
                    observedActivities++;
                }
            },
            stopped: activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.ParentSpanId == parent.SpanId)
                {
                    observedActivities++;
                }
            });
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions(), logger);

        var exception = Should.Throw<ArgumentException>(() => factory.CreateServiceProvider(services));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("containerBuilder");
        applicationFactoryCalls.ShouldBe(0);
        observedActivities.ShouldBe(0);
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void CreateServiceProvider_WhenOptionsChangeLater_UsesValuesCapturedByConstructor()
    {
        var options = new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        };
        var factory = new AgentKitServiceProviderFactory(options);
        options.ValidateOnBuild = false;
        options.ValidateScopes = false;
        var services = new ServiceCollection();
        _ = services.AddScoped<ScopedDependency>();
        _ = services.AddSingleton<SingletonCapturingScoped>();

        var exception = Should.Throw<AggregateException>(() =>
            factory.CreateServiceProvider(factory.CreateBuilder(services)));

        exception.ToString().ShouldContain("Cannot consume scoped service");
    }

    [Fact]
    public async Task CreateServiceProvider_WhenRootIsDisposed_DisposesCreatedSingletonExactlyOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TrackingDisposable>();
        var factory = new AgentKitServiceProviderFactory();
        var provider = (ServiceProvider) factory.CreateServiceProvider(factory.CreateBuilder(services));
        var singleton = provider.GetRequiredService<TrackingDisposable>();

        await provider.DisposeAsync();

        singleton.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateServiceProvider_WhenBuildSucceeds_EmitsSafeParentedActivityMetricsAndLog()
    {
        const string protectedKey = "must-not-appear";
        using var parent = new Activity("provider-build-parent").Start();
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = BuildActivityListener(
            stopped: activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.ParentSpanId == parent.SpanId)
                {
                    activities.Enqueue(activity);
                }
            });
        using var metrics = new BuildMetricCollector(parent.TraceId);
        var logger = new RecordingLogger();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<ILeaf, Leaf>(protectedKey);
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions(), logger);

        await using var provider = (ServiceProvider) factory.CreateServiceProvider(services);

        Activity.Current.ShouldBe(parent);
        var activity = activities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AgentKitActivityNames.AgentCompositionBuild);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("built");
        activity.Tags.Any(static tag =>
            tag.Value is not null && tag.Value.Contains(protectedKey, StringComparison.Ordinal)).ShouldBeFalse();
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildCount
            && measurement.Outcome == "built"
            && measurement.Value == 1);
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildDuration
            && measurement.Outcome == "built"
            && measurement.Value >= 0);
        var log = logger.Entries.ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(18004);
        log.Level.ShouldBe(LogLevel.Debug);
        log.Message.ShouldNotContain(protectedKey);
    }

    [Fact]
    public void CreateServiceProvider_WhenDeclaredGraphIsRejected_ObservesRejectionBeforeFactories()
    {
        var applicationFactoryCalls = 0;
        using var parent = new Activity("provider-rejection-parent").Start();
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = BuildActivityListener(
            stopped: activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.ParentSpanId == parent.SpanId)
                {
                    activities.Enqueue(activity);
                }
            });
        using var metrics = new BuildMetricCollector(parent.TraceId);
        var logger = new RecordingLogger();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf>(
            _ =>
            {
                applicationFactoryCalls++;
                return new Leaf();
            });
        _ = services.DeclareAgentKitComponent(Registration(ServiceLifetime.Scoped));
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions(), logger);

        var exception = Should.Throw<AgentCompositionException>(() => factory.CreateServiceProvider(services));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.lifetime-mismatch");
        applicationFactoryCalls.ShouldBe(0);
        Activity.Current.ShouldBe(parent);
        var activity = activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");
        activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(AgentCompositionException));
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildCount
            && measurement.Outcome == "rejected");
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildDuration
            && measurement.Outcome == "rejected"
            && measurement.Value >= 0);
        var log = logger.Entries.ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(18005);
        log.Properties["ErrorType"].ShouldBe(nameof(AgentCompositionException));
    }

    [Fact]
    public void CreateServiceProvider_WhenMicrosoftDiBuildFails_EmitsBoundedFailureDiagnostics()
    {
        using var parent = new Activity("provider-failure-parent").Start();
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = BuildActivityListener(
            stopped: activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.ParentSpanId == parent.SpanId)
                {
                    activities.Enqueue(activity);
                }
            });
        using var metrics = new BuildMetricCollector(parent.TraceId);
        var logger = new RecordingLogger();
        var services = new ServiceCollection();
        _ = services.AddScoped<ScopedDependency>();
        _ = services.AddSingleton<SingletonCapturingScoped>();
        var factory = new AgentKitServiceProviderFactory(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }, logger);

        _ = Should.Throw<AggregateException>(() => factory.CreateServiceProvider(services));

        Activity.Current.ShouldBe(parent);
        var activity = activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(AggregateException));
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildCount
            && measurement.Outcome == "failed");
        var log = logger.Entries.ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(18006);
        log.Properties["ErrorType"].ShouldBe(nameof(AggregateException));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateServiceProvider_WhenActivityCallbackThrows_PreservesProviderAndParentage(bool throwOnStart)
    {
        using var parent = new Activity("hostile-activity-parent").Start();
        using var listener = BuildActivityListener(
            started: activity => ThrowForExactBuild(activity, parent, throwOnStart),
            stopped: activity => ThrowForExactBuild(activity, parent, !throwOnStart));
        var factory = new AgentKitServiceProviderFactory();

        await using var provider = (ServiceProvider) factory.CreateServiceProvider(new ServiceCollection());

        _ = provider.ShouldNotBeNull();
        Activity.Current.ShouldBe(parent);
    }

    [Fact]
    public async Task CreateServiceProvider_WhenLoggerAndMeterCallbacksThrow_PreservesProviderAndDisposal()
    {
        using var parent = new Activity("hostile-meter-parent").Start();
        using var activityListener = BuildActivityListener();
        using var metricScope = new MetricTraceScope(parent.TraceId);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                && instrument.Name is AgentKitMetricNames.AgentCompositionBuildCount
                    or AgentKitMetricNames.AgentCompositionBuildDuration)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, _, _) => ThrowForExactMetric(parent.TraceId));
        meterListener.SetMeasurementEventCallback<double>((_, _, _, _) => ThrowForExactMetric(parent.TraceId));
        meterListener.Start();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TrackingDisposable>();
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions(), new ThrowingLogger());
        var provider = (ServiceProvider) factory.CreateServiceProvider(services);
        var singleton = provider.GetRequiredService<TrackingDisposable>();

        await provider.DisposeAsync();

        singleton.DisposeCount.ShouldBe(1);
        Activity.Current.ShouldBe(parent);
    }

    [Fact]
    public void CreateServiceProvider_WhenObserversThrowDuringRejection_PreservesOriginalDiagnosticAndNoFactoryEffects()
    {
        var factoryCalls = 0;
        using var parent = new Activity("hostile-rejection-parent").Start();
        using var activityListener = BuildActivityListener(
            stopped: activity => ThrowForExactBuild(activity, parent, enabled: true));
        using var metricScope = new MetricTraceScope(parent.TraceId);
        using var meterListener = ThrowingBuildMeterListener(parent.TraceId);
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf>(
            _ =>
            {
                factoryCalls++;
                return new Leaf();
            });
        _ = services.DeclareAgentKitComponent(Registration(ServiceLifetime.Scoped));
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions(), new ThrowingLogger());

        var exception = Should.Throw<AgentCompositionException>(() => factory.CreateServiceProvider(services));

        exception.Diagnostics.ShouldContain(
            static diagnostic => diagnostic.Code == "agentkit.component-registration.lifetime-mismatch");
        factoryCalls.ShouldBe(0);
        Activity.Current.ShouldBe(parent);
    }

    [Theory]
    [InlineData("initial")]
    [InlineData("negative")]
    [InlineData("elapsed")]
    public async Task CreateServiceProvider_WhenDurationClockIsUnavailableOrNegative_OmitsDurationOnly(
        string scenario)
    {
        using var parent = new Activity("unavailable-duration-parent").Start();
        using var activityListener = BuildActivityListener();
        using var metrics = new BuildMetricCollector(parent.TraceId);
        var logger = new RecordingLogger();
        TimeProvider timeProvider = scenario switch
        {
            "initial" => new ThrowingTimestampProvider(),
            "elapsed" => new EndingTimestampThrowsProvider(),
            _ => new DecreasingTimestampProvider(),
        };
        var factory = new AgentKitServiceProviderFactory(
            new ServiceProviderOptions(), logger, timeProvider);

        await using var provider = (ServiceProvider) factory.CreateServiceProvider(new ServiceCollection());

        _ = provider.ShouldNotBeNull();
        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildCount
            && measurement.Outcome == "built");
        metrics.Measurements.ShouldNotContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildDuration);
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(18004);
        Activity.Current.ShouldBe(parent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task CreateServiceProvider_WhenDurationClockSucceeds_RecordsExactSeconds(
        int elapsedSeconds)
    {
        using var parent = new Activity("exact-duration-parent").Start();
        using var activityListener = BuildActivityListener();
        using var metrics = new BuildMetricCollector(parent.TraceId);
        var factory = new AgentKitServiceProviderFactory(
            new ServiceProviderOptions(),
            new RecordingLogger(),
            new FixedElapsedTimeProvider(elapsedSeconds));

        await using var provider = (ServiceProvider) factory.CreateServiceProvider(new ServiceCollection());

        metrics.Measurements.ShouldContain(measurement =>
            measurement.Name == AgentKitMetricNames.AgentCompositionBuildDuration
            && measurement.Outcome == "built"
            && measurement.Value == elapsedSeconds);
        Activity.Current.ShouldBe(parent);
    }

    private static ComponentRegistrationDescriptor Registration(ServiceLifetime lifetime) => new(
        ComponentContractReference.Unkeyed<ILeaf>(), typeof(Leaf), lifetime, []);

    private static ActivityListener BuildActivityListener(
        Action<Activity>? started = null,
        Action<Activity>? stopped = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started,
            ActivityStopped = stopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void ThrowForExactBuild(Activity activity, Activity parent, bool enabled)
    {
        if (enabled
            && activity.OperationName == AgentKitActivityNames.AgentCompositionBuild
            && activity.TraceId == parent.TraceId
            && activity.ParentSpanId == parent.SpanId)
        {
            throw new TestObserverException();
        }
    }

    private static MeterListener ThrowingBuildMeterListener(ActivityTraceId traceId)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, value) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.AgentCompositionBuildCount
                        or AgentKitMetricNames.AgentCompositionBuildDuration)
                {
                    value.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => ThrowForExactMetric(traceId));
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => ThrowForExactMetric(traceId));
        listener.Start();
        return listener;
    }

    private static void ThrowForExactMetric(ActivityTraceId traceId)
    {
        if (_activeMetricTrace.Value == traceId)
        {
            throw new TestObserverException();
        }
    }

    private sealed class ScopedDependency;

    private sealed class SingletonCapturingScoped(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class TrackingDisposable: IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private interface ILeaf;

    private sealed class Leaf: ILeaf;

    private sealed class NullContainingServiceCollection: List<ServiceDescriptor>, IServiceCollection;

    private sealed class TestObserverException: Exception;

    private sealed class ThrowingTimestampProvider: TimeProvider
    {
        public override long GetTimestamp() => throw new TestObserverException();
    }

    private sealed class DecreasingTimestampProvider: TimeProvider
    {
        private long _timestamp = 2;

        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Decrement(ref _timestamp);
    }

    private sealed class EndingTimestampThrowsProvider: TimeProvider
    {
        private int _calls;

        public override long GetTimestamp() => Interlocked.Increment(ref _calls) == 1
            ? 1
            : throw new TestObserverException();
    }

    private sealed class FixedElapsedTimeProvider(long elapsedSeconds): TimeProvider
    {
        private int _calls;

        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Increment(ref _calls) == 1
            ? 10
            : 10 + elapsedSeconds;
    }

    private sealed class ThrowingLogger: ILogger<AgentKitServiceProviderFactory>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => throw new TestObserverException();

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new TestObserverException();
    }

    private sealed class RecordingLogger: ILogger<AgentKitServiceProviderFactory>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            _entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class BuildMetricCollector: IDisposable
    {
        private readonly ConcurrentQueue<MetricMeasurement> _measurements = new();
        private readonly MeterListener _listener = new();
        private readonly MetricTraceScope _scope;
        private readonly ActivityTraceId _traceId;

        public BuildMetricCollector(ActivityTraceId traceId)
        {
            _traceId = traceId;
            _scope = new MetricTraceScope(traceId);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.AgentCompositionBuildCount
                        or AgentKitMetricNames.AgentCompositionBuildDuration)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Record(instrument, value, tags));
            _listener.Start();
        }

        public IReadOnlyList<MetricMeasurement> Measurements => _measurements.ToArray();

        public void Dispose()
        {
            _listener.Dispose();
            _scope.Dispose();
        }

        private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct, IConvertible
        {
            if (_activeMetricTrace.Value != _traceId)
            {
                return;
            }

            var outcome = tags.ToArray().SingleOrDefault(
                static tag => tag.Key == AgentKitTagNames.Outcome).Value as string;
            _measurements.Enqueue(new MetricMeasurement(
                instrument.Name,
                Convert.ToDouble(value, CultureInfo.InvariantCulture),
                outcome));
        }
    }

    private sealed record MetricMeasurement(string Name, double Value, string? Outcome);

    private sealed class MetricTraceScope: IDisposable
    {
        private readonly ActivityTraceId? _previous = _activeMetricTrace.Value;
        private bool _disposed;

        public MetricTraceScope(ActivityTraceId traceId) => _activeMetricTrace.Value = traceId;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activeMetricTrace.Value = _previous;
        }
    }
}
