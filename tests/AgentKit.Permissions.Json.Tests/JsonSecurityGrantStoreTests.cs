// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared grant-store contract against the durable JSON adapter and verifies behavior the shared contract cannot reach.</summary>
public sealed class JsonSecurityGrantStoreTests
    : SecurityGrantStoreConformanceTests<JsonSecurityGrantStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    protected override JsonSecurityGrantStoreConformanceFixture CreateFixture() => new();

    // -- Uninitialized and disposed use ------------------------------------------------------------------------

    /// <summary>Verifies registration before initialization fails closed rather than silently accepting evidence.</summary>
    [Fact]
    public async Task RegisterAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        var grant = TestGrantFactory.CreateGrant(_now);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await store.RegisterAsync(grant, TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
    }

    /// <summary>Verifies receiptless consumption before initialization fails closed.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        var grant = TestGrantFactory.CreateGrant(_now);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () =>
            await store.ValidateAndConsumeAsync(
                grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
    }

    /// <summary>Verifies intent-aware consumption before initialization fails closed.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WithIntent_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        var grant = TestGrantFactory.CreateGrant(_now);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () =>
            await store.ValidateAndConsumeAsync(grant, TestGrantFactory.CreateEnforcement(grant),
                TestGrantFactory.CreateIntent(), TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
    }

    /// <summary>Verifies revocation before initialization fails closed.</summary>
    [Fact]
    public async Task RevokeAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(async () =>
            await store.RevokeAsync(new GrantId(Guid.NewGuid()), TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
    }

    /// <summary>Verifies repeated initialization after success is a no-op.</summary>
    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies initializing a disposed store throws rather than resurrecting it.</summary>
    [Fact]
    public async Task InitializeAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        using var root = new TestStoreRoot();
        var store = CreateStore(root.Path);
        store.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies disposal is idempotent.</summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestStoreRoot();
        var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.Dispose();

        Should.NotThrow(store.Dispose);
    }

    /// <summary>Verifies every mutating operation on a disposed store throws <see cref="ObjectDisposedException"/> instead of touching the log.</summary>
    [Fact]
    public async Task RegisterAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        using var root = new TestStoreRoot();
        var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispose();
        var grant = TestGrantFactory.CreateGrant(_now);

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await store.RegisterAsync(grant, TestContext.Current.CancellationToken));
    }

    // -- Bootstrap failure classification -----------------------------------------------------------------------

    /// <summary>Verifies opening a missing root without permission to create it fails closed.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingRootIsMissing_ThrowsInvalidOperation()
    {
        var missingRoot = Path.Combine(TestTemporaryDirectory.Create(), "missing");
        var target = new JsonSecurityGrantStoreTarget(
            missingRoot, new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);
        using var store = new JsonSecurityGrantStore(
            target, JsonSecurityGrantStoreSettings.CreateDefault(), new FakeTimeProvider(_now));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        exception.GetType().ShouldBe(typeof(InvalidOperationException));
    }

    /// <summary>Verifies opening an existing root with no manifest fails closed rather than fabricating one.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingManifestIsMissing_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
    }

    /// <summary>Verifies a manifest whose store identity differs from bootstrap configuration fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestStoreIdentityDiffers_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        using (var store = CreateStore(root.Path))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = CreateStore(
            root.Path, instanceId: new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()),
            openMode: JsonStoreOpenMode.OpenExisting);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a manifest carrying a different store-kind discriminator fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestStoreKindDiffers_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        root.WriteManifest(new JsonStoreManifest(instanceId.Value, "agentkit.permissions.approvals", 1, settings.Encoding.Fingerprint));

        using var store = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a manifest carrying an unsupported schema version fails closed rather than being misread.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionIsUnsupported_ThrowsSchemaUnsupported()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        root.WriteManifest(new JsonStoreManifest(instanceId.Value, "agentkit.permissions.grants", 99, settings.Encoding.Fingerprint));

        using var store = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
    }

    /// <summary>Verifies reopening a store under a materially different encoding contract fails closed rather than misreading existing evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractChangesMaterially_ThrowsSchemaUnsupported()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        using (var store = CreateStore(root.Path, instanceId: instanceId))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        // Property names must stay decodable so the manifest document can still be read; only a semantic option excluded
        // from decodability but included in the fingerprint (case sensitivity) changes, isolating the fingerprint check
        // from an unrelated document-decode failure.
        var differentOptions = JsonStoreSerialization.CreateCanonicalOptions();
        differentOptions.PropertyNameCaseInsensitive = true;
        var reopenSettings = new JsonSecurityGrantStoreSettings(
            1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(differentOptions));
        using var reopened = CreateStore(
            root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: reopenSettings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.SchemaUnsupported);
    }

    /// <summary>Verifies changing only presentation-only indentation is accepted because it is excluded from the fingerprint.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOnlyWriteIndentedChanges_IsAccepted()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        using (var store = CreateStore(root.Path, instanceId: instanceId))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var indentedOptions = JsonStoreSerialization.CreateCanonicalOptions();
        indentedOptions.WriteIndented = true;
        var reopenSettings = new JsonSecurityGrantStoreSettings(
            1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(indentedOptions));
        using var reopened = CreateStore(
            root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: reopenSettings);

        await Should.NotThrowAsync(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies an encoding contract that cannot reproduce the fidelity probe fails initialization rather than corrupting evidence later.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractCannotRoundTrip_ThrowsInvalidOperation()
    {
        using var root = new TestStoreRoot();
        var brokenOptions = JsonStoreSerialization.CreateCanonicalOptions();
        brokenOptions.MaxDepth = 2;
        var settings = new JsonSecurityGrantStoreSettings(1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(brokenOptions));
        using var store = CreateStore(root.Path, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        exception.GetType().ShouldBe(typeof(InvalidOperationException));
    }

    /// <summary>Verifies an underlying filesystem write failure is classified as persistence-failed rather than corrupt evidence.</summary>
    /// <remarks>
    /// The advisory lock file is pre-created so lock acquisition itself succeeds against the read-only directory; only the
    /// manifest write, which must create a new file, is denied. Unix-only because <see cref="UnixFileMode"/> permission
    /// enforcement has no equivalent on Windows.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRootCannotBeWritten_ThrowsPersistenceFailed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var root = new TestStoreRoot();
        File.WriteAllBytes(root.Combine("store.lock"), []);
        File.SetUnixFileMode(root.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            using var store = CreateStore(root.Path);

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.PersistenceFailed);
        }
        finally
        {
            File.SetUnixFileMode(
                root.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // -- Durability, recovery, and corruption -------------------------------------------------------------------

    /// <summary>Verifies remaining uses, revocation, and receipts replay exactly after a store is closed and reopened.</summary>
    [Fact]
    public async Task Store_WhenReopenedAfterDispose_ReplaysExactState()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var clock = new FakeTimeProvider(_now);
        var grant = TestGrantFactory.CreateGrant(_now, allowedUses: 3);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var intent = TestGrantFactory.CreateIntent();
        SecurityEnforcementIntentReceipt? receipt;

        using (var store = new JsonSecurityGrantStore(
            CreateTarget(root.Path, instanceId), settings, clock))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var consumed = await store.ValidateAndConsumeAsync(
                grant, enforcement, intent, TestContext.Current.CancellationToken);
            consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
            receipt = consumed.IntentReceipt;
            (await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        using var reopened = new JsonSecurityGrantStore(
            CreateTarget(root.Path, instanceId, JsonStoreOpenMode.OpenExisting), settings, clock);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        var replayed = await reopened.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        replayed.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        replayed.IntentReceipt.ShouldBe(receipt);
        var afterRevocation = await reopened.ValidateAndConsumeAsync(
            grant, enforcement, TestGrantFactory.CreateIntent(2), TestContext.Current.CancellationToken);
        afterRevocation.Status.ShouldBe(GrantConsumptionStatus.Revoked);
    }

    /// <summary>Verifies a torn trailing append is discarded under recovery mode and the store proceeds without the incomplete record.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogHasTornAppendAndRecoveryPermitted_DiscardsIncompleteRecord()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var grant = TestGrantFactory.CreateGrant(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        }

        var originalLength = root.LogBytes("grants").Length;
        var truncatedLength = root.TruncateTrailingBytes("grants", 5);
        truncatedLength.ShouldBeLessThan(originalLength);

        using var recovered = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.RecoverTornAppends, settings: settings);
        await recovered.InitializeAsync(TestContext.Current.CancellationToken);

        var result = await recovered.ValidateAndConsumeAsync(
            grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Unknown);
    }

    /// <summary>Verifies a torn trailing append is reported as corrupt evidence when only exact validation is permitted.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogHasTornAppendAndValidateExact_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var grant = TestGrantFactory.CreateGrant(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        }

        _ = root.TruncateTrailingBytes("grants", 5);

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a complete but malformed log line fails closed as corrupt evidence rather than being skipped.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogLineIsMalformed_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(TestGrantFactory.CreateGrant(_now), TestContext.Current.CancellationToken);
        }

        root.AppendLogLine("grants", "{ this is not valid json");

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a record referencing an unknown grant identity fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRecordReferencesUnknownGrant_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var orphanRecord = JsonSecurityGrantLogRecord.ForConsumption(new GrantId(Guid.NewGuid()), 0, null);
        root.AppendLogLine("grants", EncodeRecord(orphanRecord, settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a persisted remaining-use count above the grant's allowed uses fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRemainingUsesExceedsAllowedUses_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var grant = TestGrantFactory.CreateGrant(_now, allowedUses: 2);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        }

        var impossibleRecord = JsonSecurityGrantLogRecord.ForConsumption(grant.Id, 99, null);
        root.AppendLogLine("grants", EncodeRecord(impossibleRecord, settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a record whose kind the current schema does not define fails closed instead of being silently skipped.</summary>
    /// <remarks>
    /// The canonical contract's strict enum converter can never itself decode an undefined kind, so this case configures a
    /// permissive integer-tolerant converter to model a hand-edited log line under a materially different schema version,
    /// proving that <c>Apply</c> still fails closed rather than trusting the writer.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRecordKindIsUnsupported_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var permissiveOptions = JsonStoreSerialization.CreateCanonicalOptions();
        permissiveOptions.Converters.Clear();
        permissiveOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true));
        var settings = new JsonSecurityGrantStoreSettings(
            1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(permissiveOptions));
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        root.AppendLogLine("grants", /*lang=json,strict*/ "{\"kind\":99}");

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.CorruptEvidence);
    }

    /// <summary>Verifies a log whose replayed record count exceeds the compaction threshold is rewritten to its live state without changing projected behavior.</summary>
    [Fact]
    public async Task InitializeAsync_WhenReplayedRecordCountExceedsThreshold_CompactsLogWithoutChangingProjectedState()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = new JsonSecurityGrantStoreSettings(1_048_576, 1_048_576, 1, JsonEncodingSettings.CreateDefault());
        var grant = TestGrantFactory.CreateGrant(_now, allowedUses: 3);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var intent = TestGrantFactory.CreateIntent();
        SecurityEnforcementIntentReceipt? receipt;
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
            var consumed = await store.ValidateAndConsumeAsync(
                grant, enforcement, intent, TestContext.Current.CancellationToken);
            receipt = consumed.IntentReceipt.ShouldNotBeNull();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                _ = await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);
            }
        }

        var beforeCompaction = root.LogLineCount("grants");
        beforeCompaction.ShouldBe(4);

        using (var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings))
        {
            await reopened.InitializeAsync(TestContext.Current.CancellationToken);

            root.LogLineCount("grants").ShouldBeLessThan(beforeCompaction);
            var result = await reopened.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GrantConsumptionStatus.Exhausted);
            result.RemainingUses.ShouldBe(0);
        }

        // Replaying the already-compacted log on a third open decodes its State and Receipt records, proving compaction
        // output itself replays back to the identical live projection rather than only being written correctly once.
        using var thirdOpen = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);
        await thirdOpen.InitializeAsync(TestContext.Current.CancellationToken);

        var replayedReceipt = await thirdOpen.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        replayedReceipt.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        replayedReceipt.IntentReceipt.ShouldBe(receipt);
        var afterReplay = await thirdOpen.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);
        afterReplay.Status.ShouldBe(GrantConsumptionStatus.Exhausted);
    }

    // -- Exclusive lock and root safety --------------------------------------------------------------------------

    /// <summary>Verifies a second store cannot initialize over the same root while the first holds it, and succeeds once released.</summary>
    [Fact]
    public async Task InitializeAsync_WhenAnotherWriterHoldsRoot_FailsUntilReleased()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var first = CreateStore(root.Path, instanceId: instanceId, settings: settings);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
        exception.GetType().ShouldBe(typeof(InvalidOperationException));

        first.Dispose();
        await Should.NotThrowAsync(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a store root reached through a symbolic link is rejected rather than silently followed.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsSymlinked_ThrowsInvalidOperation()
    {
        var realDirectory = TestTemporaryDirectory.Create();
        var linkPath = Path.Combine(Path.GetDirectoryName(realDirectory)!, $"agentkit-json-link-{Guid.NewGuid():N}");
        _ = Directory.CreateSymbolicLink(linkPath, realDirectory);
        try
        {
            var target = new JsonSecurityGrantStoreTarget(
                linkPath, new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
            using var store = new JsonSecurityGrantStore(
                target, JsonSecurityGrantStoreSettings.CreateDefault(), new FakeTimeProvider(_now));

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

            exception.GetType().ShouldBe(typeof(InvalidOperationException));
        }
        finally
        {
            File.Delete(linkPath);
            if (Directory.Exists(realDirectory))
            {
                Directory.Delete(realDirectory, recursive: true);
            }
        }
    }

    // -- No-op appends -------------------------------------------------------------------------------------------

    /// <summary>Verifies registering the exact same grant twice appends nothing to the durable log.</summary>
    [Fact]
    public async Task RegisterAsync_WhenGrantIsAlreadyRegisteredIdentically_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);

        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var countAfterFirst = root.LogLineCount("grants");
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        root.LogLineCount("grants").ShouldBe(countAfterFirst);
    }

    /// <summary>Verifies revoking a grant identity that was never registered returns false without appending anything.</summary>
    [Fact]
    public async Task RevokeAsync_WhenGrantWasNeverRegistered_ReturnsFalse()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var revoked = await store.RevokeAsync(new GrantId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        revoked.ShouldBeFalse();
        root.LogLineCount("grants").ShouldBe(0);
    }

    /// <summary>Verifies revoking an already revoked grant appends nothing and still reports the grant exists.</summary>
    [Fact]
    public async Task RevokeAsync_WhenAlreadyRevoked_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        (await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var countAfterFirst = root.LogLineCount("grants");
        (await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();

        root.LogLineCount("grants").ShouldBe(countAfterFirst);
    }

    // -- Observability --------------------------------------------------------------------------------------------

    /// <summary>Verifies a successful operation emits the documented log event, a safe successful activity, and a bounded metric.</summary>
    [Fact]
    public async Task RegisterAsync_WhenSuccessful_EmitsSafeActivityLogAndMetric()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingSecurityGrantStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);

        Activity? stopped = null;
        var outcomes = new List<string>();
        using var activityListener = CreateActivityListener(activity => stopped = activity);
        using var meterListener = CreateMeterListener(AgentKitMetricNames.SecurityGrantStoreOperationCount, outcomes);

        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.GenAiOperationName).ShouldBe("register");
        activity.GetTagItem(AgentKitTagNames.SecurityGrantId)?.ToString().ShouldBe(grant.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.SecurityRequestId)?.ToString().ShouldBe(grant.RequestId.ToString());
        outcomes.ShouldContain("success");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19100);
        AssertNoProtectedContent(logger, activity, grant, root.Path);
    }

    /// <summary>Verifies a failed operation emits the documented failure log event and a safe failed activity without protected content.</summary>
    [Fact]
    public async Task RegisterAsync_WhenConflicting_EmitsFailureActivityLogAndMetric()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingSecurityGrantStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var conflicting = grant with { Effect = SecurityEffect.Delete };

        Activity? stopped = null;
        var outcomes = new List<string>();
        using var activityListener = CreateActivityListener(activity => stopped = activity);
        using var meterListener = CreateMeterListener(AgentKitMetricNames.SecurityGrantStoreOperationCount, outcomes);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.RegisterAsync(conflicting, TestContext.Current.CancellationToken));

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        outcomes.ShouldContain("faulted");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19101);
        AssertNoProtectedContent(logger, activity, grant, root.Path);
    }

    /// <summary>Verifies use before initialization is observed with the unavailable outcome and its bounded failure classification.</summary>
    [Fact]
    public async Task RegisterAsync_WhenUninitialized_EmitsUnavailableActivityAndLog()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingSecurityGrantStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        var grant = TestGrantFactory.CreateGrant(_now);

        Activity? stopped = null;
        using var activityListener = CreateActivityListener(activity => stopped = activity);

        _ = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
            async () => await store.RegisterAsync(grant, TestContext.Current.CancellationToken));

        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("unavailable");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19101);
    }

    /// <summary>Verifies a throwing logger never changes the semantic outcome of a store operation.</summary>
    [Fact]
    public async Task RegisterAsync_WhenLoggerThrows_PreservesRegistration()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path, logger: new ThrowingSecurityGrantStoreLogger());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);

        await Should.NotThrowAsync(async () => await store.RegisterAsync(grant, TestContext.Current.CancellationToken));
        var result = await store.ValidateAndConsumeAsync(
            grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    /// <summary>Verifies a disabled or absent diagnostics listener does not change the store's behavior or result.</summary>
    [Fact]
    public async Task RegisterAsync_WhenNoListenerIsAttached_BehavesIdentically()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var grant = TestGrantFactory.CreateGrant(_now);

        await Should.NotThrowAsync(async () => await store.RegisterAsync(grant, TestContext.Current.CancellationToken));
        var result = await store.ValidateAndConsumeAsync(
            grant, TestGrantFactory.CreateEnforcement(grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    // -- Shared helpers ------------------------------------------------------------------------------------------

    private static JsonSecurityGrantStoreTarget CreateTarget(
        string path,
        JsonSecurityGrantStoreInstanceId? instanceId = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends) => new(
            path, instanceId ?? new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()), openMode, recoveryMode);

    private static JsonSecurityGrantStore CreateStore(
        string path,
        JsonSecurityGrantStoreInstanceId? instanceId = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends,
        JsonSecurityGrantStoreSettings? settings = null,
        ILogger<JsonSecurityGrantStore>? logger = null) => new(
            CreateTarget(path, instanceId, openMode, recoveryMode),
            settings ?? JsonSecurityGrantStoreSettings.CreateDefault(),
            new FakeTimeProvider(_now),
            logger);

    private static byte[] EncodeRecordBytes(JsonSecurityGrantLogRecord record, JsonSecurityGrantStoreSettings settings) =>
        JsonStoreSerialization.Encode(record, settings.Encoding.RecordOptions, settings.MaximumRecordBytes);

    private static string EncodeRecord(JsonSecurityGrantLogRecord record, JsonSecurityGrantStoreSettings settings) =>
        Encoding.UTF8.GetString(EncodeRecordBytes(record, settings));

    private static ActivityListener CreateActivityListener(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityGrantStoreOperation)
                {
                    onStopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener CreateMeterListener(string instrumentName, List<string> outcomes)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, subscription) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == instrumentName)
                {
                    subscription.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => outcomes.Add(OutcomeFrom(tags)));
        listener.Start();
        return listener;
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllData;

    private static string OutcomeFrom(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.Outcome)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("The grant-store metric omitted its bounded outcome tag.");
    }

    private static void AssertNoProtectedContent(
        RecordingSecurityGrantStoreLogger logger, Activity activity, SecurityGrant grant, string storeRootPath)
    {
        logger.Events.ShouldAllBe(item =>
            !item.Message.Contains(grant.InputFingerprint.Value, StringComparison.Ordinal)
            && !item.Message.Contains(storeRootPath, StringComparison.Ordinal));
        foreach (var tag in activity.TagObjects)
        {
            var text = tag.Value?.ToString();
            if (text is null)
            {
                continue;
            }

            text.ShouldNotContain(grant.InputFingerprint.Value);
            text.ShouldNotContain(storeRootPath);
        }
    }
}
