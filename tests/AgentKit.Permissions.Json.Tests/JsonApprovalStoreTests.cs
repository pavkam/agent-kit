// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared approval-store contract against the durable JSON adapter and verifies behavior the shared contract cannot reach.</summary>
public sealed class JsonApprovalStoreTests: ApprovalStoreConformanceTests<JsonApprovalStoreConformanceFixture>
{
    private const string _failureKindKey = "agentkit.failure_kind";
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    protected override JsonApprovalStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies the durable JSON approval store declares durable, trusted-control-plane capabilities.</summary>
    [Fact]
    public void Capabilities_WhenAccessed_DeclaresDurableTrustedControlPlane()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);

        store.Capabilities.IsDurable.ShouldBeTrue();
        store.Capabilities.ProvidesTrustedControlPlane.ShouldBeTrue();
    }

    // -- Uninitialized and disposed use ------------------------------------------------------------------------

    /// <summary>Verifies creation before initialization fails closed rather than silently accepting evidence.</summary>
    [Fact]
    public async Task CreateAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        var request = TestApprovalFactory.CreateRequest(_now);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("open_failed");
    }

    /// <summary>Verifies resolution before initialization fails closed.</summary>
    [Fact]
    public async Task ResolveAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.ResolveAsync(response, TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("open_failed");
    }

    /// <summary>Verifies reading before initialization fails closed.</summary>
    [Fact]
    public async Task ReadAsync_WhenUninitialized_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.ReadAsync(
                new ApprovalRequestId(Guid.NewGuid()), TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("open_failed");
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
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

        store.Dispose();

        Should.NotThrow(store.Dispose);
    }

    /// <summary>Verifies a mutating operation on a disposed store throws <see cref="ObjectDisposedException"/> instead of touching the log.</summary>
    [Fact]
    public async Task CreateAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        using var root = new TestStoreRoot();
        var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        store.Dispose();
        var request = TestApprovalFactory.CreateRequest(_now);

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));
    }

    // -- Bootstrap failure classification -----------------------------------------------------------------------

    /// <summary>Verifies opening a missing root without permission to create it fails closed.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingRootIsMissing_ThrowsInvalidOperation()
    {
        var missingRoot = Path.Combine(TestTemporaryDirectory.Create(), "missing");
        var target = new JsonApprovalStoreTarget(
            missingRoot, new JsonApprovalStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);
        using var store = new JsonApprovalStore(
            target, JsonApprovalStoreSettings.CreateDefault(), new FakeTimeProvider(_now));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBeNull();
    }

    /// <summary>Verifies opening an existing root with no manifest fails closed rather than fabricating one.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingManifestIsMissing_ThrowsUnavailable()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("open_failed");
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
            root.Path, instanceId: new JsonApprovalStoreInstanceId(Guid.NewGuid()),
            openMode: JsonStoreOpenMode.OpenExisting);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a manifest carrying a different store-kind discriminator fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestStoreKindDiffers_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        root.WriteManifest(new JsonStoreManifest(instanceId.Value, "agentkit.permissions.grants", 1, settings.Encoding.Fingerprint));

        using var store = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a manifest carrying an unsupported schema version fails closed rather than being misread.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionIsUnsupported_ThrowsSchemaUnsupported()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        root.WriteManifest(new JsonStoreManifest(instanceId.Value, "agentkit.permissions.approvals", 99, settings.Encoding.Fingerprint));

        using var store = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("schema_unsupported");
    }

    /// <summary>Verifies reopening a store under a materially different encoding contract fails closed rather than misreading existing evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractChangesMaterially_ThrowsSchemaUnsupported()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        using (var store = CreateStore(root.Path, instanceId: instanceId))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        // Property names must stay decodable so the manifest document can still be read; only a semantic option excluded
        // from decodability but included in the fingerprint (case sensitivity) changes, isolating the fingerprint check
        // from an unrelated document-decode failure.
        var differentOptions = JsonStoreSerialization.CreateCanonicalOptions();
        differentOptions.PropertyNameCaseInsensitive = true;
        var reopenSettings = new JsonApprovalStoreSettings(
            1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(differentOptions));
        using var reopened = CreateStore(
            root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: reopenSettings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("schema_unsupported");
    }

    /// <summary>Verifies changing only presentation-only indentation is accepted because it is excluded from the fingerprint.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOnlyWriteIndentedChanges_IsAccepted()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        using (var store = CreateStore(root.Path, instanceId: instanceId))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var indentedOptions = JsonStoreSerialization.CreateCanonicalOptions();
        indentedOptions.WriteIndented = true;
        var reopenSettings = new JsonApprovalStoreSettings(
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
        var settings = new JsonApprovalStoreSettings(1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(brokenOptions));
        using var store = CreateStore(root.Path, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBeNull();
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

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

            FailureKindOf(exception).ShouldBe("persistence_failed");
        }
        finally
        {
            File.SetUnixFileMode(
                root.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Verifies a caller-cancelled token before the creation append begins throws cancellation without appending evidence.</summary>
    [Fact]
    public async Task CreateAsync_WhenCallerAlreadyCancelled_ThrowsOperationCanceled()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await store.CreateAsync(request, source.Token));

        root.LogLineCount("approvals").ShouldBe(0);
    }

    // -- Durability, recovery, and corruption -------------------------------------------------------------------

    /// <summary>Verifies a pending request and its terminal resolution replay exactly after a store is closed and reopened.</summary>
    [Fact]
    public async Task Store_WhenReopenedAfterDispose_ReplaysExactState()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var clock = new FakeTimeProvider(_now);
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);

        using (var store = new JsonApprovalStore(CreateTarget(root.Path, instanceId), settings, clock))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            (await store.CreateAsync(request, TestContext.Current.CancellationToken))
                .ShouldBe(ApprovalStoreCreateResult.Created);
            (await store.ResolveAsync(response, TestContext.Current.CancellationToken))
                .ShouldBe(ApprovalStoreResolveResult.Resolved);
        }

        using var reopened = new JsonApprovalStore(
            CreateTarget(root.Path, instanceId, JsonStoreOpenMode.OpenExisting), settings, clock);
        await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

        var read = await reopened.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        read.Request.ShouldBe(request);
        read.Response.ShouldBe(response);
    }

    /// <summary>Verifies a torn trailing append is discarded under recovery mode and the store proceeds without the incomplete record.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogHasTornAppendAndRecoveryPermitted_DiscardsIncompleteRecord()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var request = TestApprovalFactory.CreateRequest(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        }

        var originalLength = root.LogBytes("approvals").Length;
        var truncatedLength = root.TruncateTrailingBytes("approvals", 5);
        truncatedLength.ShouldBeLessThan(originalLength);

        using var recovered = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.RecoverTornAppends, settings: settings);
        await recovered.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

        var read = await recovered.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        read.Request.ShouldBeNull();
        read.Response.ShouldBeNull();
    }

    /// <summary>Verifies a torn trailing append is reported as corrupt evidence when only exact validation is permitted.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogHasTornAppendAndValidateExact_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var request = TestApprovalFactory.CreateRequest(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        }

        _ = root.TruncateTrailingBytes("approvals", 5);

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a complete but malformed log line fails closed as corrupt evidence rather than being skipped.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogLineIsMalformed_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(TestApprovalFactory.CreateRequest(_now), TestContext.Current.CancellationToken);
        }

        root.AppendLogLine("approvals", "{ this is not valid json");

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a resolution record referencing an unknown request fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenResolutionReferencesUnknownRequest_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var orphanRequest = TestApprovalFactory.CreateRequest(
            _now, requestId: new ApprovalRequestId(Guid.NewGuid()));
        var orphanResponse = TestApprovalFactory.CreateResponse(orphanRequest);
        root.AppendLogLine("approvals", EncodeRecord(JsonApprovalLogRecord.ForResolution(orphanResponse), settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a request already carrying a terminal resolution that receives a second resolution record fails closed as corrupt evidence.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRequestHasMoreThanOneResolution_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
            _ = await store.ResolveAsync(response, TestContext.Current.CancellationToken);
        }

        var secondResponse = TestApprovalFactory.CreateResponse(
            request, resolution: ApprovalResolution.Denied, responseId: new ApprovalResponseId(Guid.NewGuid()));
        root.AppendLogLine("approvals", EncodeRecord(JsonApprovalLogRecord.ForResolution(secondResponse), settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a resolution whose binding does not match its request's binding fails closed as corrupt evidence rather than widening authority.</summary>
    [Fact]
    public async Task InitializeAsync_WhenResolutionBindingDoesNotMatchRequest_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var request = TestApprovalFactory.CreateRequest(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        }

        var mismatchedBinding = TestApprovalFactory.CreateBinding(_now, inputFingerprint: "sha256:different");
        var mismatchedResponse = new ApprovalResponse(
            new ApprovalResponseId(Guid.NewGuid()), request.Id, mismatchedBinding, ApprovalResolution.Approved,
            TestApprovalFactory.CreateApproverIdentity(_now), request.CreatedAt.AddSeconds(1));
        root.AppendLogLine("approvals", EncodeRecord(JsonApprovalLogRecord.ForResolution(mismatchedResponse), settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a second creation record under an identity already created fails closed as corrupt evidence rather than being merged.</summary>
    [Fact]
    public async Task InitializeAsync_WhenIdentityHasMoreThanOneCreationRecord_ThrowsCorruptEvidence()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var request = TestApprovalFactory.CreateRequest(_now);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        }

        root.AppendLogLine("approvals", EncodeRecord(JsonApprovalLogRecord.ForCreation(request), settings));

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
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
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var permissiveOptions = JsonStoreSerialization.CreateCanonicalOptions();
        permissiveOptions.Converters.Clear();
        permissiveOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true));
        var settings = new JsonApprovalStoreSettings(
            1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(permissiveOptions));
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        root.AppendLogLine("approvals", /*lang=json,strict*/ "{\"kind\":99}");

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));

        FailureKindOf(exception).ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a log whose replayed record count exceeds the compaction threshold is rewritten to its live state without changing projected behavior.</summary>
    [Fact]
    public async Task InitializeAsync_WhenReplayedRecordCountExceedsThreshold_CompactsLogWithoutChangingProjectedState()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = new JsonApprovalStoreSettings(1_048_576, 1_048_576, 1, JsonEncodingSettings.CreateDefault());
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);
        using (var store = CreateStore(root.Path, instanceId: instanceId, settings: settings))
        {
            await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
            _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
            _ = await store.ResolveAsync(response, TestContext.Current.CancellationToken);
        }

        var beforeCompaction = root.LogLineCount("approvals");
        beforeCompaction.ShouldBe(2);

        using var reopened = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);
        await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);

        root.LogLineCount("approvals").ShouldBe(beforeCompaction);
        var read = await reopened.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        read.Request.ShouldBe(request);
        read.Response.ShouldBe(response);
    }

    // -- Exclusive lock and root safety --------------------------------------------------------------------------

    /// <summary>Verifies a second store cannot initialize over the same root while the first holds it, and succeeds once released.</summary>
    [Fact]
    public async Task InitializeAsync_WhenAnotherWriterHoldsRoot_FailsUntilReleased()
    {
        using var root = new TestStoreRoot();
        var instanceId = new JsonApprovalStoreInstanceId(Guid.NewGuid());
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var first = CreateStore(root.Path, instanceId: instanceId, settings: settings);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = CreateStore(root.Path, instanceId: instanceId, openMode: JsonStoreOpenMode.OpenExisting, settings: settings);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
        FailureKindOf(exception).ShouldBeNull();

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
            var target = new JsonApprovalStoreTarget(
                linkPath, new JsonApprovalStoreInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
            using var store = new JsonApprovalStore(
                target, JsonApprovalStoreSettings.CreateDefault(), new FakeTimeProvider(_now));

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                async () => await store.InitializeAsync(TestContext.Current.CancellationToken));

            FailureKindOf(exception).ShouldBeNull();
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

    /// <summary>Verifies creating the exact same request twice appends nothing to the durable log.</summary>
    [Fact]
    public async Task CreateAsync_WhenRequestAlreadyExistsIdentically_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);

        (await store.CreateAsync(request, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.Created);
        var countAfterFirst = root.LogLineCount("approvals");
        (await store.CreateAsync(request, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.AlreadyExists);

        root.LogLineCount("approvals").ShouldBe(countAfterFirst);
    }

    /// <summary>Verifies a conflicting request under an existing identity is rejected and appends nothing.</summary>
    [Fact]
    public async Task CreateAsync_WhenRequestConflicts_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var countAfterFirst = root.LogLineCount("approvals");
        var conflicting = TestApprovalFactory.CreateRequest(_now, requestId: request.Id, safePresentation: "Different text");

        (await store.CreateAsync(conflicting, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.Conflict);

        root.LogLineCount("approvals").ShouldBe(countAfterFirst);
    }

    /// <summary>Verifies resolving an unknown request appends nothing.</summary>
    [Fact]
    public async Task ResolveAsync_WhenRequestIsNotFound_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);
        var countBefore = root.LogLineCount("approvals");

        (await store.ResolveAsync(response, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreResolveResult.NotFound);

        root.LogLineCount("approvals").ShouldBe(countBefore);
    }

    /// <summary>Verifies resolving an already resolved request with the identical response appends nothing.</summary>
    [Fact]
    public async Task ResolveAsync_WhenAlreadyResolvedIdentically_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var response = TestApprovalFactory.CreateResponse(request);
        (await store.ResolveAsync(response, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreResolveResult.Resolved);
        var countAfterFirst = root.LogLineCount("approvals");

        (await store.ResolveAsync(response, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreResolveResult.AlreadyResolved);

        root.LogLineCount("approvals").ShouldBe(countAfterFirst);
    }

    /// <summary>Verifies a different terminal decision for an already resolved request is a conflict and appends nothing.</summary>
    [Fact]
    public async Task ResolveAsync_WhenAlreadyResolvedWithDifferentDecision_AppendsNothing()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var response = TestApprovalFactory.CreateResponse(request);
        _ = await store.ResolveAsync(response, TestContext.Current.CancellationToken);
        var countAfterFirst = root.LogLineCount("approvals");
        var different = TestApprovalFactory.CreateResponse(
            request, resolution: ApprovalResolution.Denied, responseId: new ApprovalResponseId(Guid.NewGuid()));

        (await store.ResolveAsync(different, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreResolveResult.Conflict);

        root.LogLineCount("approvals").ShouldBe(countAfterFirst);
    }

    // -- Observability --------------------------------------------------------------------------------------------

    /// <summary>Verifies a successful operation emits the documented log event, a safe successful activity, and a bounded metric.</summary>
    [Fact]
    public async Task CreateAsync_WhenSuccessful_EmitsSafeActivityLogAndMetric()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingApprovalStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);

        Activity? stopped = null;
        var outcomes = new List<string>();
        using var activityListener = CreateActivityListener(activity => stopped = activity);
        using var meterListener = CreateMeterListener(AgentKitMetricNames.SecurityApprovalStoreOperationCount, outcomes);

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.GenAiOperationName).ShouldBe("create");
        activity.GetTagItem(AgentKitTagNames.SecurityRequestId)?.ToString()
            .ShouldBe(request.Binding.Request.Id.ToString());
        outcomes.ShouldContain("created");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19110);
        AssertNoProtectedContent(logger, activity, request, root.Path);
    }

    /// <summary>Verifies use before initialization is observed with the unavailable outcome and its bounded failure classification.</summary>
    [Fact]
    public async Task CreateAsync_WhenUninitialized_EmitsUnavailableActivityAndLog()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingApprovalStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        var request = TestApprovalFactory.CreateRequest(_now);

        Activity? stopped = null;
        using var activityListener = CreateActivityListener(activity => stopped = activity);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));

        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("unavailable");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19111);
    }

    /// <summary>Verifies a conflicting resolution emits the documented failure-shaped outcome without protected content.</summary>
    [Fact]
    public async Task ResolveAsync_WhenConflicting_EmitsBoundedOutcomeActivityAndLog()
    {
        using var root = new TestStoreRoot();
        var logger = new RecordingApprovalStoreLogger();
        using var store = CreateStore(root.Path, logger: logger);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);
        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var response = TestApprovalFactory.CreateResponse(request);
        _ = await store.ResolveAsync(response, TestContext.Current.CancellationToken);
        var conflicting = TestApprovalFactory.CreateResponse(
            request, resolution: ApprovalResolution.Denied, responseId: new ApprovalResponseId(Guid.NewGuid()));

        Activity? stopped = null;
        using var activityListener = CreateActivityListener(activity => stopped = activity);

        _ = await store.ResolveAsync(conflicting, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("conflict");
        logger.Events.ShouldContain(static item => item.EventId.Id == 19111);
        AssertNoProtectedContent(logger, activity, request, root.Path);
    }

    /// <summary>Verifies a throwing logger never changes the semantic outcome of a store operation.</summary>
    [Fact]
    public async Task CreateAsync_WhenLoggerThrows_PreservesCreation()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path, logger: new ThrowingApprovalStoreLogger());
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);

        await Should.NotThrowAsync(async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));
        var read = await store.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        read.Request.ShouldBe(request);
    }

    /// <summary>Verifies a disabled or absent diagnostics listener does not change the store's behavior or result.</summary>
    [Fact]
    public async Task CreateAsync_WhenNoListenerIsAttached_BehavesIdentically()
    {
        using var root = new TestStoreRoot();
        using var store = CreateStore(root.Path);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = TestApprovalFactory.CreateRequest(_now);

        await Should.NotThrowAsync(async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));
        var read = await store.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        read.Request.ShouldBe(request);
    }

    // -- Shared helpers ------------------------------------------------------------------------------------------

    private static JsonApprovalStoreTarget CreateTarget(
        string path,
        JsonApprovalStoreInstanceId? instanceId = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends) => new(
            path, instanceId ?? new JsonApprovalStoreInstanceId(Guid.NewGuid()), openMode, recoveryMode);

    private static JsonApprovalStore CreateStore(
        string path,
        JsonApprovalStoreInstanceId? instanceId = null,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends,
        JsonApprovalStoreSettings? settings = null,
        ILogger<JsonApprovalStore>? logger = null) => new(
            CreateTarget(path, instanceId, openMode, recoveryMode),
            settings ?? JsonApprovalStoreSettings.CreateDefault(),
            new FakeTimeProvider(_now),
            logger);

    private static string EncodeRecord(JsonApprovalLogRecord record, JsonApprovalStoreSettings settings) =>
        Encoding.UTF8.GetString(
            JsonStoreSerialization.Encode(record, settings.Encoding.RecordOptions, settings.MaximumRecordBytes));

    private static string? FailureKindOf(Exception exception) => exception.Data[_failureKindKey] as string;

    private static ActivityListener CreateActivityListener(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityApprovalStoreOperation)
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

        throw new InvalidOperationException("The approval-store metric omitted its bounded outcome tag.");
    }

    private static void AssertNoProtectedContent(
        RecordingApprovalStoreLogger logger, Activity activity, ApprovalRequest request, string storeRootPath)
    {
        logger.Events.ShouldAllBe(item =>
            !item.Message.Contains(request.SafePresentation, StringComparison.Ordinal)
            && !item.Message.Contains(storeRootPath, StringComparison.Ordinal));
        foreach (var tag in activity.TagObjects)
        {
            var text = tag.Value?.ToString();
            if (text is null)
            {
                continue;
            }

            text.ShouldNotContain(request.SafePresentation);
            text.ShouldNotContain(storeRootPath);
        }
    }
}
