// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics;

using Microsoft.Extensions.Logging;

public sealed class PortableSessionEntryCodecBoundaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutionLaneCodecConstructor_WhenTimeProviderIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits
            ? Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(
                null!, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>(), Limits()))
            : Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(
                null!, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>()));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutionLaneCodecConstructor_WhenLoggerIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits
            ? Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(
                TimeProvider.System, null!, Limits()))
            : Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(
                TimeProvider.System, null!));

        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void ExecutionLaneCodecConstructor_WhenLimitsIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(
            TimeProvider.System, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>(), null!));

        exception.ParamName.ShouldBe("limits");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InputPromotedCodecConstructor_WhenTimeProviderIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits
            ? Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(
                null!, new CountingLogger<InputPromotedSessionEntryCodec>(), Limits()))
            : Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(
                null!, new CountingLogger<InputPromotedSessionEntryCodec>()));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InputPromotedCodecConstructor_WhenLoggerIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits
            ? Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(
                TimeProvider.System, null!, Limits()))
            : Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(
                TimeProvider.System, null!));

        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void InputPromotedCodecConstructor_WhenLimitsIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(
            TimeProvider.System, new CountingLogger<InputPromotedSessionEntryCodec>(), null!));

        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ExecutionLaneCodec_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingTimeProvider();
        var logger = new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var codec = new ExecutionLaneProvisionedSessionEntryCodec(
            clock, logger);

        AssertNoActivity(() =>
        {
            Should.Throw<ArgumentNullException>(() => codec.Encode(null!)).ParamName.ShouldBe("entry");
            Should.Throw<ArgumentNullException>(() => codec.Decode(null!)).ParamName.ShouldBe("wire");
        });
        clock.TimestampCalls.ShouldBe(0);
        logger.IsEnabledCalls.ShouldBe(0);
        logger.LogCalls.ShouldBe(0);
    }

    [Fact]
    public void InputPromotedCodec_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingTimeProvider();
        var logger = new CountingLogger<InputPromotedSessionEntryCodec>();
        var codec = new InputPromotedSessionEntryCodec(
            clock, logger);

        AssertNoActivity(() =>
        {
            Should.Throw<ArgumentNullException>(() => codec.Encode(null!)).ParamName.ShouldBe("entry");
            Should.Throw<ArgumentNullException>(() => codec.Decode(null!)).ParamName.ShouldBe("wire");
        });
        clock.TimestampCalls.ShouldBe(0);
        logger.IsEnabledCalls.ShouldBe(0);
        logger.LogCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BoundedWriteStreamConstructor_WhenCapacityIsNotPositive_ThrowsExactParameterName(int capacity)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedWriteStream(capacity));

        exception.ParamName.ShouldBe("capacity");
    }

    [Fact]
    public void BoundedWriteStreamWrite_WhenArgumentsAreInvalid_ThrowsExactParameterName()
    {
        using var stream = new BoundedWriteStream(4);
        var bytes = new byte[2];

        Should.Throw<ArgumentNullException>(() => stream.Write(null!, 0, 0)).ParamName.ShouldBe("buffer");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, -1, 0)).ParamName.ShouldBe("offset");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 0, -1)).ParamName.ShouldBe("count");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 3, 0)).ParamName.ShouldBe("offset");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 1, 2)).ParamName.ShouldBe("count");
        stream.WrittenSpan.ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void BoundedWriteStreamWrite_WhenFillingRemainingCapacity_PreservesBytesAndRejectsLaterWriteWithoutMutation()
    {
        using var stream = new BoundedWriteStream(4);

        stream.Write([1, 2], 0, 2);
        stream.Write([3, 4], 0, 2);
        stream.WrittenSpan.ToArray().ShouldBe([1, 2, 3, 4]);

        _ = Should.Throw<InvalidOperationException>(() => stream.Write([5], 0, 1));
        stream.WrittenSpan.ToArray().ShouldBe([1, 2, 3, 4]);
    }

    private static SessionEntryCodecLimits Limits() => new(1024, 1, 128, 4);

    private static void AssertNoActivity(Action action)
    {
        using var parent = new Activity("portable.codec.boundary").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentTraceId = parent.TraceId;
        var parentSpanId = parent.SpanId;
        var started = false;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) =>
                options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context
                    ? ActivitySamplingResult.PropagationData
                    : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec
                    && activity.ParentSpanId == parentSpanId && activity.TraceId == parentTraceId)
                {
                    started = true;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        action();

        started.ShouldBeFalse();
    }

    private sealed class CountingTimeProvider: TimeProvider
    {
        public int TimestampCalls { get; private set; }

        public override long GetTimestamp()
        {
            TimestampCalls++;
            return 0;
        }
    }

    private sealed class CountingLogger<T>: ILogger<T>
    {
        public int IsEnabledCalls { get; private set; }

        public int LogCalls { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            IsEnabledCalls++;
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => LogCalls++;
    }
}
