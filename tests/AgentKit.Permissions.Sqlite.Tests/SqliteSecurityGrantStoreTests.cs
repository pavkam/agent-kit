// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Verifies SqliteSecurityGrantStore behavior and contracts.</summary>
public sealed class SqliteSecurityGrantStoreTests: SecurityGrantStoreConformanceTests<SqliteSecurityGrantStoreConformanceFixture>
{
    /// <summary>Verifies hostile standard diagnostics callbacks cannot change a committed receipt or its replay.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenObserversThrow_PreservesConsumptionAndReplay()
    {
        var directory = CreateDirectory();
        try
        {
            using var parent = new Activity("sqlite-hostile-observer-test").Start();
            var traceId = parent.TraceId;
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
                Sample = SampleAllData,
                ActivityStarted = activity => ThrowForStoreActivity(activity, traceId),
                ActivityStopped = activity => ThrowForStoreActivity(activity, traceId),
            };
            ActivitySource.AddActivityListener(activityListener);
            using var meterListener = new MeterListener
            {
                InstrumentPublished = static (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.SecurityGrantStoreOperationCount)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            {
                if (Activity.Current?.TraceId == traceId)
                {
                    throw new InvalidOperationException("meter failure");
                }
            });
            meterListener.Start();
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true, new ThrowingLogger());
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            var enforcement = TestGrantFactory.CreateEnforcement(grant);
            var intent = TestGrantFactory.CreateIntent();
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var consumed = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            var replay = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            replay.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static void ThrowForStoreActivity(Activity activity, ActivityTraceId traceId)
        {
            if (activity.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && activity.TraceId == traceId)
            {
                throw new InvalidOperationException("activity failure");
            }
        }
    }

    /// <summary>Verifies measurement-clock failure neither changes grant semantics nor fabricates a duration.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenMeasurementClockFails_PreservesResultWithoutDuration()
    {
        var directory = CreateDirectory();
        try
        {
            var counts = 0;
            var durations = 0;
            using var parent = new Activity("sqlite-grant-store-clock-test").Start();
            var parentSpanId = parent.SpanId;
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
                Sample = SampleAllData,
            };
            ActivitySource.AddActivityListener(activityListener);
            using var listener = new MeterListener
            {
                InstrumentPublished = static (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SecurityGrantStoreOperationCount or AgentKitMetricNames.SecurityGrantStoreOperationDuration)
                    {
                        meterListener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                if (Activity.Current?.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && Activity.Current.ParentSpanId == parentSpanId && HasOperation(tags, "consume"))
                {
                    _ = Interlocked.Increment(ref counts);
                }
            });
            listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            {
                if (Activity.Current?.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && Activity.Current.ParentSpanId == parentSpanId && HasOperation(tags, "consume"))
                {
                    _ = Interlocked.Increment(ref durations);
                }
            });
            listener.Start();
            var now = DateTimeOffset.UnixEpoch;
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), new ThrowingMeasurementTimeProvider(now), create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(now);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var result = await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            Volatile.Read(ref counts).ShouldBe(1);
            Volatile.Read(ref durations).ShouldBe(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static bool HasOperation(ReadOnlySpan<KeyValuePair<string, object?>> tags, string expected)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == AgentKitTagNames.GenAiOperationName)
                {
                    return string.Equals(tag.Value?.ToString(), expected, StringComparison.Ordinal);
                }
            }

            return false;
        }
    }

    /// <summary>Verifies cancellation raised after clock evaluation rolls back both capacity and intent insertion.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenClockCancelsCaller_RollsBackTransaction()
    {
        var directory = CreateDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            var now = DateTimeOffset.UnixEpoch;
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var path = Path.Combine(directory, "grants.db");
            var store = CreateStore(path, instanceId, new CancellingTimeProvider(now, cancellation), create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(now);
            var enforcement = TestGrantFactory.CreateEnforcement(grant);
            var intent = TestGrantFactory.CreateIntent();
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ValidateAndConsumeAsync(grant, enforcement, intent, cancellation.Token));
            var reopened = CreateStore(path, instanceId, new FakeTimeProvider(now), create: false);
            await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var consumed = await reopened.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            consumed.RemainingUses.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies semantic grant results produce bounded outcomes and truthful activity status.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenGrantIsExpired_ObservesBoundedErrorOutcome()
    {
        var directory = CreateDirectory();
        try
        {
            Activity? stopped = null;
            var counts = new ConcurrentQueue<string>();
            var durations = new ConcurrentQueue<string>();
            using var parent = new Activity("sqlite-grant-store-observation-test").Start();
            var parentSpanId = parent.SpanId;
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
                Sample = SampleAllData,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && activity.ParentSpanId == parentSpanId && IsConsumeFamily(activity.GetTagItem(AgentKitTagNames.GenAiOperationName)?.ToString()))
                    {
                        stopped = activity;
                    }
                },
            };
            ActivitySource.AddActivityListener(activityListener);
            using var meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SecurityGrantStoreOperationCount or AgentKitMetricNames.SecurityGrantStoreOperationDuration)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                if (Activity.Current?.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && Activity.Current.ParentSpanId == parentSpanId && HasConsumeFamilyOperation(tags))
                {
                    counts.Enqueue(ReadOutcome(tags));
                }
            });
            meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            {
                if (Activity.Current?.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation && Activity.Current.ParentSpanId == parentSpanId && HasConsumeFamilyOperation(tags))
                {
                    durations.Enqueue(ReadOutcome(tags));
                }
            });
            meterListener.Start();
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMinutes(10));
            var result = await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GrantConsumptionStatus.Expired);
            var activity = stopped.ShouldNotBeNull();
            activity.Status.ShouldBe(ActivityStatusCode.Error);
            activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("expired");
            activity.GetTagItem(AgentKitTagNames.SecurityRequestId).ShouldBe(grant.RequestId.ToString());
            activity.TagObjects.Select(static item => item.Value).ShouldNotContain(grant.InputFingerprint.Value);
            counts.ShouldBe(["expired"]);
            durations.ShouldBe(["expired"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static string ReadOutcome(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == AgentKitTagNames.Outcome)
                {
                    return tag.Value?.ToString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }

    /// <summary>Verifies source-generated terminal logs retain bounded fields and truthful severity without exporting protected evidence.</summary>
    [Fact]
    public async Task GrantStoreOperations_WhenTerminating_EmitSafeStructuredEvents()
    {
        var directory = CreateDirectory();
        try
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var logger = new RecordingLogger();
            var path = Path.Combine(directory, "grants.db");
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true, logger);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            var enforcement = TestGrantFactory.CreateEnforcement(grant);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var consumed = await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMinutes(10));
            var expired = await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ValidateAndConsumeAsync(grant, enforcement, cancellation.Token));
            File.Delete(path);
            _ = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken));
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            expired.Status.ShouldBe(GrantConsumptionStatus.Expired);
            var entries = logger.Snapshot().Where(static entry => entry.Properties.TryGetValue("Operation", out var operation) && IsConsumeFamily(operation?.ToString())).ToArray();
            entries.Select(static entry => (entry.EventId.Id, entry.Level, entry.Properties["Outcome"]?.ToString())).ShouldBe([
                (19000, LogLevel.Debug, "consumed"),
                (19000, LogLevel.Debug, "consumed"),
                (19001, LogLevel.Warning, "expired"),
                (19001, LogLevel.Warning, "cancelled"),
                (19001, LogLevel.Warning, "unavailable"),
            ]);
            foreach (var entry in entries)
            {
                entry.Exception.ShouldBeNull();
                entry.Properties.Keys.All(static key => key is "Operation" or "Outcome" or "FailureKind" or "SecurityRequestId" or "GrantId" or "IntentId" or "{OriginalFormat}").ShouldBeTrue();
                entry.Properties["SecurityRequestId"]?.ToString().ShouldBe(grant.RequestId.ToString());
                entry.Properties["GrantId"]?.ToString().ShouldBe(grant.Id.ToString());
                entry.Message.ShouldNotContain(directory);
                entry.Message.ShouldNotContain(grant.InputFingerprint.Value);
                entry.Properties.Values.Select(static value => value?.ToString()).ShouldNotContain(grant.InputFingerprint.Value);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies create-and-migrate bootstrap recovers the only safe interrupted-create state.</summary>
    [Fact]
    public async Task InitializeAsync_WhenInterruptedCreateLeftEmptyDatabase_RecoversExactTarget()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
            }

            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "PRAGMA application_id;").ShouldBe(0x414B5047L);
            ExecuteScalar(verification, "PRAGMA user_version;").ShouldBe(1L);
            ExecuteScalar(verification, "PRAGMA journal_mode;").ShouldBe("wal");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies pre-cancelled bootstrap does not create or open the configured target.</summary>
    [Fact]
    public async Task InitializeAsync_WhenPreCancelled_LeavesTargetAbsent()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.InitializeAsync(cancellation.Token));
            File.Exists(path).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a nonempty foreign target is rejected before header or journal mutation.</summary>
    [Fact]
    public async Task InitializeAsync_WhenExistingDatabaseIsForeign_RejectsWithoutMutation()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "foreign.db");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE foreign_state(value INTEGER NOT NULL);";
                _ = command.ExecuteNonQuery();
            }

            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "PRAGMA application_id;").ShouldBe(0L);
            ExecuteScalar(verification, "PRAGMA user_version;").ShouldBe(0L);
            ExecuteScalar(verification, "PRAGMA journal_mode;").ShouldBe("delete");
            ExecuteScalar(verification, "SELECT COUNT(*) FROM sqlite_schema WHERE name = 'foreign_state';").ShouldBe(1L);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies repeated and competing bootstrap calls converge on one exact persistent identity.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRepeatedConcurrently_ReplaysOneExactStore()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var stores = Enumerable.Range(0, 8).Select(_ => CreateStore(path, instanceId, clock, create: true)).ToArray();
            await Task.WhenAll(stores.Select(store => Task.Run(async () => await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken))));
            await stores[0].InitializeTrustedAsync(clock, TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await stores[0].RegisterAsync(grant, TestContext.Current.CancellationToken);
            var result = await stores[^1].ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies an initialized target with another expected identity is rejected without changing its header or journal mode.</summary>
    [Fact]
    public async Task InitializeAsync_WhenExpectedIdentityChanges_RejectsBeforeMutation()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var first = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            await first.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            using (var changedJournal = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                changedJournal.Open();
                ExecuteScalar(changedJournal, "PRAGMA journal_mode = DELETE;").ShouldBe("delete");
            }

            var second = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await second.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "PRAGMA application_id;").ShouldBe(0x414B5047L);
            ExecuteScalar(verification, "PRAGMA user_version;").ShouldBe(1L);
            ExecuteScalar(verification, "PRAGMA journal_mode;").ShouldBe("delete");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies extra executable or query schema objects are rejected before protected row access.</summary>
    [Theory]
    [InlineData("CREATE TRIGGER unexpected_trigger AFTER UPDATE ON security_grants BEGIN SELECT 1; END;")]
    [InlineData("CREATE VIEW unexpected_view AS SELECT grant_id FROM security_grants;")]
    [InlineData("CREATE INDEX unexpected_index ON security_grants(remaining_uses);")]
    public async Task RegisterAsync_WhenSchemaContainsUnexpectedObject_RejectsBeforeAccess(string sql)
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                _ = command.ExecuteNonQuery();
            }

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(clock.GetUtcNow()), TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "SELECT COUNT(*) FROM security_grants;").ShouldBe(0L);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies even a dangling SQLite sidecar link is rejected before protected state is opened.</summary>
    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public async Task RegisterAsync_WhenSidecarIsDanglingLink_RejectsBeforeAccess(string suffix)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = File.CreateSymbolicLink(path + suffix, Path.Combine(directory, "missing-sidecar"));
            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
            File.Delete(path + suffix);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "SELECT COUNT(*) FROM security_grants;").ShouldBe(0L);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies bootstrap never attaches an orphaned sidecar to a newly created main database.</summary>
    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public async Task InitializeAsync_WhenOrphanedSidecarExists_RejectsBeforeMainCreation(string suffix)
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            await File.WriteAllBytesAsync(path + suffix, [1], TestContext.Current.CancellationToken);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);
            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
            File.Exists(path).ShouldBeFalse();
            File.ReadAllBytes(path + suffix).ShouldBe([1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a second adapter instance reconstructs the authoritative receipt and never authorizes it again.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenStoreReopens_ReconcilesPersistedReceipt()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            var enforcement = TestGrantFactory.CreateEnforcement(grant);
            var intent = TestGrantFactory.CreateIntent();
            var first = CreateStore(path, instanceId, clock, create: true);
            await first.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            await first.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var consumed = await first.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            var reopened = CreateStore(path, instanceId, clock, create: false);
            await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var reconciled = await reopened.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            reconciled.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
            reconciled.IntentReceipt.ShouldBe(consumed.IntentReceipt);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies historical receipt lookup cannot bypass authoritative validation of the presented grant.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenHistoricalIntentUsesTamperedGrant_RejectsBeforeReconciliation()
    {
        var directory = CreateDirectory();
        try
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            var enforcement = TestGrantFactory.CreateEnforcement(grant);
            var intent = TestGrantFactory.CreateIntent();
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            _ = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
            var result = await store.ValidateAndConsumeAsync(grant with { Effect = SecurityEffect.Delete }, enforcement, intent, TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GrantConsumptionStatus.Tampered);
            result.IntentReceipt.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies timestamp offsets are retained but registration equality follows domain instant equality.</summary>
    [Fact]
    public async Task RegisterAsync_WhenSameInstantsUseDifferentOffsets_AcceptsDomainEquivalentReplay()
    {
        var directory = CreateDirectory();
        try
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            var replay = grant with
            {
                NotBefore = grant.NotBefore.ToOffset(TimeSpan.FromHours(2)),
                ExpiresAt = grant.ExpiresAt.ToOffset(TimeSpan.FromHours(2)),
            };
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            await store.RegisterAsync(replay, TestContext.Current.CancellationToken);
            var consumed = await store.ValidateAndConsumeAsync(replay, TestGrantFactory.CreateEnforcement(replay), TestContext.Current.CancellationToken);
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies impossible stored capacity is reported as corruption rather than trusted or leaked.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenRemainingUsesExceedsGrant_RejectsCorruptEvidence()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE security_grants SET remaining_uses = 999;";
                _ = command.ExecuteNonQuery();
            }

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies an oversized persisted evidence cell is rejected by length before materialization or capacity mutation.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenPersistedGrantBlobExceedsBound_RejectsWithoutConsumption()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE security_grants SET payload = zeroblob(1048577), payload_digest = zeroblob(32);";
                _ = command.ExecuteNonQuery();
            }

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () => await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));
            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
            using var verification = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            verification.Open();
            ExecuteScalar(verification, "SELECT remaining_uses FROM security_grants;").ShouldBe((long) grant.AllowedUses);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies oversized concrete evidence is rejected before the adapter opens its target.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenEnforcementExceedsBound_RejectsBeforeStorageAccess()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = new SqliteSecurityGrantStore(new SqliteSecurityGrantStoreTarget(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), new SqliteSecurityGrantStoreSettings(TimeSpan.FromSeconds(1), 1_048_576, 64, 32, 32, 8), clock);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            File.Delete(path);
            var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));
            exception.ParamName.ShouldBe("enforcement");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies malformed copied grant evidence is rejected before reserving its authoritative identifier.</summary>
    [Fact]
    public async Task RegisterAsync_WhenGrantCannotBeReconstructed_RejectsBeforeRowMutation()
    {
        var directory = CreateDirectory();
        try
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch);
            // SecurityGrant's own init accessors now reject a default Audience directly, so this
            // probes RegisterAsync's own codec round-trip rejection with a mutation SecurityGrant
            // still allows to construct: a null element inside a non-default, non-empty Resources array.
            var exception = await Should.ThrowAsync<ArgumentException>(async () => await store.RegisterAsync(grant with { Resources = [null!] }, TestContext.Current.CancellationToken));
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var result = await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("grant");
            result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            result.RemainingUses.ShouldBe(grant.AllowedUses - 1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies consuming a grant that was never registered fails closed as unknown rather than as tampered or absent.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenGrantWasNeverRegistered_ReturnsUnknown()
    {
        var directory = CreateDirectory();
        try
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(Path.Combine(directory, "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());

            var result = await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);

            result.Status.ShouldBe(GrantConsumptionStatus.Unknown);
            result.IntentReceipt.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies bootstrap over an existing schema-less target that disallows creation fails closed before mutation.</summary>
    [Fact]
    public async Task InitializeAsync_WhenExistingTargetIsUninitializedAndCreationIsDisallowed_RejectsAsSchemaUnsupported()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
            }

            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a database corrupted after initialization fails a subsequent bootstrap integrity check.</summary>
    [Fact]
    public async Task InitializeAsync_WhenDatabaseIsCorruptedAfterInitialization_RejectsAsCorruptEvidence()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var store = CreateStore(path, instanceId, TimeProvider.System, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Overstate the header's declared page count (big-endian bytes 28-31) so every ordinary table scan still
            // succeeds against real, undamaged pages while PRAGMA quick_check reports the file as inconsistent.
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(28, 4), 1_000);
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            var reopened = CreateStore(path, instanceId, TimeProvider.System, create: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies exact-validation bootstrap requires WAL journaling even when identity and schema shape match.</summary>
    [Fact]
    public async Task InitializeAsync_WhenJournalModeIsNotWalUnderExactValidation_RejectsAsSchemaUnsupported()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var first = CreateStore(path, instanceId, TimeProvider.System, create: true);
            await first.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                ExecuteScalar(connection, "PRAGMA journal_mode = DELETE;").ShouldBe("delete");
            }

            var second = CreateStore(path, instanceId, TimeProvider.System, create: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await second.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a missing parent directory is rejected before any file or connection is opened.</summary>
    [Fact]
    public async Task RegisterAsync_WhenParentDirectoryDoesNotExist_RejectsAsOpenFailed()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "missing-parent", "grants.db");
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a symlinked ancestor directory in the target's path is rejected before any connection is opened.</summary>
    [Fact]
    public async Task RegisterAsync_WhenAnAncestorDirectoryIsASymbolicLink_RejectsAsOpenFailed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = CreateDirectory();
        try
        {
            var realParent = Path.Combine(directory, "real-parent");
            _ = Directory.CreateDirectory(realParent);
            var linkedParent = Path.Combine(directory, "linked-parent");
            _ = Directory.CreateSymbolicLink(linkedParent, realParent);
            var path = Path.Combine(linkedParent, "grants.db");
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a main database path that is itself a symbolic link is rejected before any connection is opened.</summary>
    [Fact]
    public async Task RegisterAsync_WhenTheMainDatabasePathIsASymbolicLink_RejectsAsOpenFailed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = CreateDirectory();
        try
        {
            var realPath = Path.Combine(directory, "real-grants.db");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = realPath }.ConnectionString))
            {
                connection.Open();
            }

            var linkedPath = Path.Combine(directory, "linked-grants.db");
            _ = File.CreateSymbolicLink(linkedPath, realPath);
            var store = CreateStore(linkedPath, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a target path occupied by a directory maps the provider's cannot-open failure to a typed result.</summary>
    [Fact]
    public async Task InitializeAsync_WhenTargetPathIsADirectory_RejectsAsOpenFailed()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            _ = Directory.CreateDirectory(path);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), TimeProvider.System, create: true);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
            var inner = exception.InnerException.ShouldBeOfType<SqliteException>();
            inner.SqliteErrorCode.ShouldBe(14);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies quick-check corruption invisible to ordinary reads still fails bootstrap integrity validation.</summary>
    [Fact]
    public async Task InitializeAsync_WhenFreelistAccountingIsInconsistent_RejectsAsCorruptEvidence()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
            var store = CreateStore(path, instanceId, TimeProvider.System, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Claim one freelist page exists (bytes 36-39) without a freelist trunk pointer (bytes 32-35), which
            // ordinary table scans and our own bootstrap pragmas never traverse but PRAGMA quick_check validates.
            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(36, 4), 1);
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            var reopened = CreateStore(path, instanceId, TimeProvider.System, create: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
            exception.SafeMessage.ShouldBe("The SQLite grant-store integrity check failed during bootstrap validation.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a persisted payload whose digest no longer matches its bytes is rejected as corrupt evidence.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenPersistedDigestDoesNotMatchPayload_RejectsAsCorruptEvidence()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE security_grants SET payload_digest = randomblob(32);";
                _ = command.ExecuteNonQuery();
            }

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a persisted payload that matches its digest but no longer decodes is mapped to corrupt evidence.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenPersistedPayloadIsUndecodableButDigestMatches_RejectsAsCorruptEvidence()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            var store = CreateStore(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), clock, create: true);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            var grant = TestGrantFactory.CreateGrant(clock.GetUtcNow());
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
            {
                connection.Open();
                using var readCommand = connection.CreateCommand();
                readCommand.CommandText = "SELECT payload FROM security_grants;";
                var payload = (byte[]) readCommand.ExecuteScalar()!;
                payload[5]++; // Corrupt the codec version byte immediately after the fixed magic.
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE security_grants SET payload = $payload, payload_digest = $digest;";
                _ = updateCommand.Parameters.AddWithValue("$payload", payload);
                _ = updateCommand.Parameters.AddWithValue("$digest", System.Security.Cryptography.SHA256.HashData(payload));
                _ = updateCommand.ExecuteNonQuery();
            }

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a target locked exclusively by another connection is reported as a bounded busy failure.</summary>
    [Fact]
    public async Task RegisterAsync_WhenAnotherConnectionHoldsAnExclusiveLock_RejectsAsBusy()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "grants.db");
            var settings = new SqliteSecurityGrantStoreSettings(TimeSpan.FromSeconds(1), 1_048_576, 1_048_576, 256, 256, 32);
            var store = new SqliteSecurityGrantStore(
                new SqliteSecurityGrantStoreTarget(path, new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
                settings,
                TimeProvider.System);
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            using var blocker = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString);
            blocker.Open();
            // BEGIN IMMEDIATE acquires a RESERVED write lock immediately, without requiring any write statement, so the
            // adapter's own immediate-transaction open on a second connection blocks until the configured timeout elapses.
            using var blockingTransaction = blocker.BeginTransaction(deferred: false);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.RegisterAsync(TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.Busy);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SqliteSecurityGrantStore CreateStore(string path, SqliteSecurityGrantStoreInstanceId instanceId, TimeProvider clock, bool create, ILogger<SqliteSecurityGrantStore>? logger = null) => new(new SqliteSecurityGrantStoreTarget(path, instanceId, create ? SqliteDatabaseOpenMode.CreateIfMissing : SqliteDatabaseOpenMode.OpenExisting, create ? SqliteSchemaMode.ApplyKnownMigrations : SqliteSchemaMode.ValidateExact), SqliteSecurityGrantStoreSettings.CreateDefault(), clock, logger);
    private static string CreateDirectory() => TestTemporaryDirectory.Create();
    private static object? ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
    private sealed class ThrowingLogger: ILogger<SqliteSecurityGrantStore>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger failure");
    }

    private sealed class RecordingLogger: ILogger<SqliteSecurityGrantStore>
    {
        private readonly ConcurrentQueue<(LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> Properties, Exception? Exception, string Message)> _entries = [];
        internal ImmutableArray<(LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> Properties, Exception? Exception, string Message)> Snapshot() => [.. _entries];
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            IReadOnlyDictionary<string, object?> properties = state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToDictionary(static item => item.Key, static item => item.Value) : ImmutableDictionary<string, object?>.Empty;
            _entries.Enqueue((logLevel, eventId, properties, exception, formatter(state, exception)));
        }
    }

    private sealed class CancellingTimeProvider(DateTimeOffset now, CancellationTokenSource cancellation): TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            cancellation.Cancel();
            return now;
        }
    }

    private sealed class ThrowingMeasurementTimeProvider(DateTimeOffset now): TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override long GetTimestamp() => throw new InvalidOperationException("measurement clock failure");
    }

    private static bool IsConsumeFamily(string? operation) =>
        operation is "consume" or "consume_assess";

    private static bool HasConsumeFamilyOperation(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.GenAiOperationName && IsConsumeFamily(tag.Value?.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    protected override SqliteSecurityGrantStoreConformanceFixture CreateFixture() => new();
    /// <summary>Verifies store construction rejects each missing immutable collaborator before effects.</summary>
    [Fact]
    public void Constructor_WhenStoreDependencyIsNull_ThrowsExactArgument()
    {
        var target = new SqliteSecurityGrantStoreTarget(Path.Combine(Path.GetTempPath(), "grants.db"), new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var settings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var targetException = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantStore(null!, settings, TimeProvider.System));
        var settingsException = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantStore(target, null!, TimeProvider.System));
        var timeException = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantStore(target, settings, null!));
        targetException.ParamName.ShouldBe("target");
        settingsException.ParamName.ShouldBe("settings");
        timeException.ParamName.ShouldBe("timeProvider");
    }
}
