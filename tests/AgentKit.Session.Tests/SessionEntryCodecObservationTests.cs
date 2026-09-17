// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Verifies SessionEntryCodecObservation behavior and contracts.</summary>
public sealed class SessionEntryCodecObservationTests
{
    private static readonly SchemaVersion Version = new("1");
    [Fact]
    public void Observation_WhenSemanticActionFaults_PreservesExceptionAndLogsOnlyNormalizedType()
    {
        using var parent = new Activity("portable.codec.fault").Start();
        Activity? stopped = null;
        using var listener = Listener(parent, activity => stopped = activity);
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var expected = new InvalidOperationException("protected-content");
        var actual = Should.Throw<InvalidOperationException>(() => SessionEntryCodecObservation.Observe<SessionEntryEncodeResult>(TimeProvider.System, logger, "execution-lane-provisioned.encode", LaneEntry(), () => throw expected));
        actual.ShouldBeSameAs(expected);
        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Error);
        stopped.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(InvalidOperationException));
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6012);
        logger.Entries[0].Message.ShouldNotContain("protected-content");
    }

    [Fact]
    public void Observe_WhenActionReturnsOpaqueResult_RecordsOpaqueOutcome()
    {
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var wire = new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), Version, [1]);

        var result = SessionEntryCodecObservation.Observe<SessionEntryDecodeResult>(TimeProvider.System, logger,
            "test.opaque", null, () => new SessionEntryOpaque(wire));

        _ = result.ShouldBeOfType<SessionEntryOpaque>();
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6009);
    }

    [Fact]
    public void Observe_WhenResultTypeIsUnrecognized_RecordsUnknownOutcome()
    {
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();

        var result = SessionEntryCodecObservation.Observe(TimeProvider.System, logger, "test.unknown", null,
            () => new UnknownDecodeResult());

        _ = result.ShouldBeOfType<UnknownDecodeResult>();
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6009);
    }

    [Fact]
    public void Observe_WhenDecodeActionFaultsWithoutInitialEvidence_LogsUncorrelatedFault()
    {
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var expected = new InvalidOperationException("decode failed");

        var actual = Should.Throw<InvalidOperationException>(() =>
            SessionEntryCodecObservation.Observe<SessionEntryDecodeResult>(TimeProvider.System, logger,
                "test.decode-fault", null, () => throw expected));

        actual.ShouldBeSameAs(expected);
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6010);
    }

    [Fact]
    public void Observe_WhenEvidenceIsAnUnrecognizedSessionEntryType_TagsWithoutLaneOrRun()
    {
        using var parent = new Activity("portable.codec.unrecognized").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec
                && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var evidence = TestFactory.MessageEntry(
            new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))), new BranchId(Id(5)), 1);

        var result = SessionEntryCodecObservation.Observe<SessionEntryEncodeResult>(TimeProvider.System, logger,
            "test.unrecognized-evidence", evidence, () => new SessionEntryEncodeRejected("not configured"));

        _ = result.ShouldBeOfType<SessionEntryEncodeRejected>();
        stopped.ShouldNotBeNull().GetTagItem(AgentKitTagNames.ExecutionLaneId).ShouldBeNull();
    }

    private sealed record UnknownDecodeResult: SessionEntryDecodeResult;

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static ActivityListener Listener(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem(AgentKitTagNames.SessionEntryCodecOperation), "execution-lane-provisioned.encode") && activity.ParentSpanId == parent.SpanId && activity.TraceId == parent.TraceId)
                {
                    stopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ExecutionLaneProvisionedSessionEntry LaneEntry(string profileKey = "default", string configurationFingerprint = "sha256:test") => new(new SessionEntryId(Id(1)), new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))), new BeforeRunOperationCorrelation(new OperationId(Id(4)), null), new BranchId(Id(5)), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, Version, new ExecutionLaneId(Id(6)), new SessionLaneRevision(1), new SessionProfileReference(new SessionProfileKey(profileKey), new SessionProfileVersion(1)), new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash(configurationFingerprint)));
    private sealed class CollectingLogger<T>: ILogger<T>
    {
        public List<(EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> indexable)
            {
                // Exercises the source-generated structured state's indexer and non-generic enumerator,
                // matching how a real structured-logging exporter (for example OpenTelemetry) walks tags.
                for (var index = 0; index < indexable.Count; index++)
                {
                    _ = indexable[index];
                }

                foreach (var _ in (System.Collections.IEnumerable) indexable)
                {
                }
            }

            Entries.Add((eventId, formatter(state, exception)));
        }
    }
}
