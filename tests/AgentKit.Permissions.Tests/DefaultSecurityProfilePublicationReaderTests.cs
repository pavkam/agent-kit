// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

/// <summary>Verifies DefaultSecurityProfilePublicationReader behavior and contracts.</summary>
public sealed class DefaultSecurityProfilePublicationReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenExactCoordinatesArePublished_ReturnsThatPublication()
    {
        var publication = Publication();
        var reader = new DefaultSecurityProfilePublicationReader([publication]);
        var result = await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, publication.ConfigurationVersion, publication.ProfileKey, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<SecurityProfilePublicationFound>().Publication.ShouldBeSameAs(publication);
    }

    [Fact]
    public async Task ReadAsync_WhenAnyCoordinateDiffers_ReturnsUnavailableWithoutLatestOrUnkeyedFallback()
    {
        var publication = Publication();
        var reader = new DefaultSecurityProfilePublicationReader([publication]);
        var queries = new[]
        {
            (new AgentId(Guid.Parse("b9999999-9999-9999-9999-999999999999")), publication.AgentDefinitionRevision, publication.ConfigurationVersion, publication.ProfileKey),
            (publication.AgentId, new AgentDefinitionRevision(publication.AgentDefinitionRevision.Value + 1), publication.ConfigurationVersion, publication.ProfileKey),
            (publication.AgentId, publication.AgentDefinitionRevision, new ConfigurationVersion(publication.ConfigurationVersion.Value + 1), publication.ProfileKey),
            (publication.AgentId, publication.AgentDefinitionRevision, publication.ConfigurationVersion, new SecurityProfileKey("security.other")),
        };
        foreach (var (agentId, definitionRevision, configurationVersion, profileKey) in queries)
        {
            var result = await reader.ReadAsync(agentId, definitionRevision, configurationVersion, profileKey, TestContext.Current.CancellationToken);
            _ = result.ShouldBeOfType<SecurityProfilePublicationUnavailable>();
        }
    }

    [Fact]
    public async Task Constructor_WhenRegistrationCollectionChanges_KeepsTheFrozenOriginalSnapshot()
    {
        var original = Publication();
        var later = Publication(profileKey: new SecurityProfileKey("security.later"));
        var registrations = new List<SecurityProfilePublication>
        {
            original
        };
        var reader = new DefaultSecurityProfilePublicationReader(registrations);
        registrations.Clear();
        registrations.Add(later);
        var originalResult = await reader.ReadAsync(original.AgentId, original.AgentDefinitionRevision, original.ConfigurationVersion, original.ProfileKey, TestContext.Current.CancellationToken);
        var laterResult = await reader.ReadAsync(later.AgentId, later.AgentDefinitionRevision, later.ConfigurationVersion, later.ProfileKey, TestContext.Current.CancellationToken);
        originalResult.ShouldBeOfType<SecurityProfilePublicationFound>().Publication.ShouldBeSameAs(original);
        _ = laterResult.ShouldBeOfType<SecurityProfilePublicationUnavailable>();
    }

    [Fact]
    public async Task ReadAsync_WhenArgumentsAreInvalid_ValidatesBeforeCancellationWithExactParameterNames()
    {
        var reader = new DefaultSecurityProfilePublicationReader([]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await AssertExactAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(default, DefinitionRevision(), ConfigurationVersion(), ProfileKey(), cancellation.Token), "agentId");
        await AssertExactAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(AgentId(), DefinitionRevision(), default, ProfileKey(), cancellation.Token), "configurationVersion");
        await AssertExactAsync<ArgumentNullException>(() => reader.ReadAsync(AgentId(), DefinitionRevision(), ConfigurationVersion(), default, cancellation.Token), "profileKey");
    }

    [Fact]
    public async Task ReadAsync_WhenCallerCancels_PropagatesCancellation()
    {
        var reader = new DefaultSecurityProfilePublicationReader([Publication()]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await reader.ReadAsync(AgentId(), DefinitionRevision(), ConfigurationVersion(), ProfileKey(), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public void Constructor_WhenPublicationCoordinatesAreDuplicated_ThrowsForPublications()
    {
        var publication = Publication();
        var exception = Should.Throw<ArgumentException>(() => new DefaultSecurityProfilePublicationReader([publication, Publication(profileVersion: new SecurityProfileVersion(99)),]));
        exception.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void ThrowIfDuplicateSecurityProfilePublication_WhenSnapshotIsValidOrEmpty_DoesNotThrow()
    {
        IReadOnlyList<SecurityProfilePublication> empty = [];
        IReadOnlyList<SecurityProfilePublication> distinct = [Publication(), Publication(profileKey: new SecurityProfileKey("security.other"))];
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(empty));
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(distinct));
    }

    [Fact]
    public void ThrowIfDuplicateSecurityProfilePublication_WhenSnapshotHasDuplicate_ReportsInferredAndExplicitParameterNames()
    {
        IReadOnlyList<SecurityProfilePublication> publications = [Publication(), Publication(profileVersion: new SecurityProfileVersion(99))];
        var inferred = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(publications));
        var explicitName = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(publications, "profilePublications"));
        inferred.ParamName.ShouldBe(nameof(publications));
        explicitName.ParamName.ShouldBe("profilePublications");
    }

    [Fact]
    public void ThrowIfDuplicateSecurityProfilePublication_WhenSnapshotOrEntryIsNull_ThrowsArgumentNullException()
    {
        var nullSnapshot = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(null!));
        IReadOnlyList<SecurityProfilePublication> nullEntry = [null!];
        var nullPublication = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfDuplicateSecurityProfilePublication(nullEntry));
        nullSnapshot.GetType().ShouldBe(typeof(ArgumentNullException));
        nullSnapshot.ParamName.ShouldBe("null");
        nullPublication.GetType().ShouldBe(typeof(ArgumentNullException));
        nullPublication.ParamName.ShouldBe(nameof(nullEntry));
    }

    [Fact]
    public void Constructor_WhenPublicationsAreNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultSecurityProfilePublicationReader(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void RecordProfilePublicationRead_WhenOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordProfilePublicationRead((SecurityProfilePublicationReadOutcome) 99));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void ToStableValue_WhenPublicationReadOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((SecurityProfilePublicationReadOutcome) 99).ToStableValue());
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public async Task ReadAsync_WhenObserved_EmitsStructuredTerminalLogsAndBoundedCount()
    {
        var outcomes = new List<string>();
        var tags = new List<KeyValuePair<string, object?>>();
        using var meterListener = MeterListenerForPublicationReads((measurement, measurementTags) =>
        {
            measurement.ShouldBe(1);
            outcomes.Add(OutcomeFrom(measurementTags));
            tags.AddRange(measurementTags.ToArray());
        });
        var logger = new RecordingLogger();
        var publication = Publication();
        var reader = new DefaultSecurityProfilePublicationReader([publication], logger);
        _ = await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, publication.ConfigurationVersion, publication.ProfileKey, TestContext.Current.CancellationToken);
        _ = await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, new ConfigurationVersion(publication.ConfigurationVersion.Value + 1), publication.ProfileKey, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, publication.ConfigurationVersion, publication.ProfileKey, cancellation.Token));
        outcomes.ShouldBe(["found", "unavailable", "cancelled"]);
        tags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
        logger.EventIds.ShouldBe([5021, 5022, 5023]);
        logger.Messages.ShouldContain(static message => message.Contains("Found", StringComparison.Ordinal));
        logger.Messages.ShouldContain(static message => message.Contains("unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_WhenLoggerOrMeterCallbackThrows_PreservesLookupResults()
    {
        var publication = Publication();
        var reader = new DefaultSecurityProfilePublicationReader([publication], new ThrowingLogger());
        using var meterListener = MeterListenerForPublicationReads(static (_, _) => throw new InvalidOperationException("observer"));
        var found = await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, publication.ConfigurationVersion, publication.ProfileKey, TestContext.Current.CancellationToken);
        var unavailable = await reader.ReadAsync(publication.AgentId, publication.AgentDefinitionRevision, new ConfigurationVersion(publication.ConfigurationVersion.Value + 1), publication.ProfileKey, TestContext.Current.CancellationToken);
        _ = found.ShouldBeOfType<SecurityProfilePublicationFound>();
        _ = unavailable.ShouldBeOfType<SecurityProfilePublicationUnavailable>();
    }

    private static SecurityProfilePublication Publication(SecurityProfileKey? profileKey = null, SecurityProfileVersion? profileVersion = null) => new(AgentId(), DefinitionRevision(), ConfigurationVersion(), profileKey ?? ProfileKey(), profileVersion ?? new SecurityProfileVersion(4), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("b2222222-2222-2222-2222-222222222222")), new SecurityPolicyVersion(5), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority.primary"));
    private static AgentId AgentId() => new(Guid.Parse("b1111111-1111-1111-1111-111111111111"));
    private static AgentDefinitionRevision DefinitionRevision() => new(2);
    private static ConfigurationVersion ConfigurationVersion() => new(3);
    private static SecurityProfileKey ProfileKey() => new("security.primary");
    private static MeterListener MeterListenerForPublicationReads(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>> onCount)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.SecurityProfilePublicationReadCount)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityProfilePublicationReadCount)
            {
                onCount(measurement, tags);
            }
        });
        listener.Start();
        return listener;
    }

    private static string OutcomeFrom(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.Outcome)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Publication-read metric did not include its bounded outcome tag.");
    }

    private static async Task AssertExactAsync<TException>(Func<ValueTask<SecurityProfilePublicationResult>> action, string parameterName)
        where TException : ArgumentException
    {
        var exception = await Should.ThrowAsync<TException>(async () => await action());
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private sealed class RecordingLogger: ILogger<DefaultSecurityProfilePublicationReader>
    {
        public List<int> EventIds { get; } = [];
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            EventIds.Add(eventId.Id);
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultSecurityProfilePublicationReader>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => throw new InvalidOperationException("observer");
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
