// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

using System.Text;

using AgentKit.Observability;

/// <summary>Verifies the file-system adapter routes every effect through the protected contracts and recovers, compacts, and releases correctly.</summary>
public sealed class FileSystemArtifactStoreTests
{
    private static readonly byte[] _content = "durable bytes"u8.ToArray();

    [Fact]
    public async Task Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var target = fixture.Target;
        var settings = FileSystemArtifactSettings.CreateDefault();
        var authorities = new FixedSecurityAuthoritySelector(fixture.Authority);
        var requestIds = new SequentialRequestIds();
        var fileIds = new FileIds();
        var intents = new SequentialIntentIds();

        FileSystemArtifactStore Build(
            FileSystemArtifactTarget? t = null, FileSystemArtifactSettings? s = null, IFileSystemSelector? selector = null,
            ISecurityAuthoritySelector? authority = null, IIdentifierGenerator<SecurityRequestId>? requests = null,
            IIdentifierGenerator<FileOperationId>? files = null, ISecurityGrantStore? grants = null,
            IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null, TimeProvider? time = null) =>
            new(t ?? target, s ?? settings, selector ?? fixture.Selector, authority ?? authorities, requests ?? requestIds,
                files ?? fileIds, grants ?? fixture.GrantStore, intentIds ?? intents, time ?? fixture.ClockProvider);

        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(null!, settings, fixture.Selector, authorities, requestIds, fileIds, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, null!, fixture.Selector, authorities, requestIds, fileIds, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, null!, authorities, requestIds, fileIds, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("fileSystems");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, null!, requestIds, fileIds, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("authorities");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, authorities, null!, fileIds, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("requestIds");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, authorities, requestIds, null!, fixture.GrantStore, intents, TimeProvider.System)).ParamName.ShouldBe("fileOperationIds");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, authorities, requestIds, fileIds, null!, intents, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, authorities, requestIds, fileIds, fixture.GrantStore, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactStore(target, settings, fixture.Selector, authorities, requestIds, fileIds, fixture.GrantStore, intents, null!)).ParamName.ShouldBe("time");
        Build().Dispose();
    }

    [Fact]
    public async Task SecurityAudience_WhenRead_NamesTheFileSystemBackend()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();

        (await fixture.CreateAsync(TestContext.Current.CancellationToken)).SecurityAudience.ShouldBe(new ComponentId("agentkit.artifacts.file-system"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.PrepareAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.FinalizeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.AbortAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Constructor_WhenBuilt_PerformsNoEffect()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();

        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenStaged_AuthorizesEachFileEffectWithAnExplicitDispositionUnderTheOperationsAuthorization()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        var payload = FileSystemArtifactLayout.PayloadName(request.TenantId, request.ContentHash);
        var requests = fixture.Authority.Requests;
        requests.Count.ShouldBe(4);
        requests[0].Kind.ShouldBe(SecurityOperationKind.FileRead);
        requests[0].Effect.ShouldBe(SecurityEffect.Observe);
        requests[1].Kind.ShouldBe(SecurityOperationKind.FileWrite);
        requests[1].Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        requests[1].Resources.ShouldBe([FileSecurityBinding.Resource(new FileTarget(new FileRootId("artifacts"), new NormalizedRelativePath(payload)))]);
        requests[2].Effect.ShouldBe(SecurityEffect.Append);
        requests[3].Effect.ShouldBe(SecurityEffect.Create);
        requests.ShouldAllBe(recorded => recorded.Audience == fixture.Volume.SecurityAudience && recorded.Authorization == request.Grant.Authorization);
    }

    [Fact]
    public async Task Reopen_WhenContentWasFinalized_RetainsBytesAndAuthoritativeMetadata()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);

        var reopened = fixture.Reopen();

        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task Reopen_WhenContentWasOnlyPrepared_KeepsStagingFinalizable()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var receipt = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();

        var reopened = fixture.Reopen();
        var finalize = fixture.CreateFinalize(receipt.PreparationId, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);

        (await ReadTextAsync(fixture, reopened, (await reopened.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalized>().Reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task DeleteAsync_WhenTheLastReferenceIsDeleted_TruncatesThePayloadAndTheTombstoneSurvivesReopen()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        var payload = FileSystemArtifactLayout.PayloadName(reference.TenantId, reference.Integrity.ContentHash);
        (await Effects(fixture).ReadAsync(Authorization(fixture), payload, 1_024, TestContext.Current.CancellationToken)).ShouldBe(_content);

        _ = await DeleteAsync(fixture, store, reference);

        (await Effects(fixture).ReadAsync(Authorization(fixture), payload, 1_024, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var reopened = fixture.Reopen();
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        (await reopened.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenAnotherReferenceSharesTheBytes_KeepsThePayload()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = await CommitAsync(fixture, store);
        var second = await CommitAsync(fixture, store);

        _ = await DeleteAsync(fixture, store, first);

        (await ReadTextAsync(fixture, store, second)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task PrepareAsync_WhenTheWriterProfileIsUnavailable_ReportsUnavailableAndStagesNothing()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        fixture.Selector.WriterUnavailable = true;
        var request = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        fixture.Selector.WriterUnavailable = false;
        var retry = fixture.CreatePrepare(_content, preparationId: request.PreparationId, artifactId: request.ArtifactId);
        await fixture.RegisterGrantAsync(retry.Grant, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        _ = (await store.PrepareAsync(retry, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenTheAuthorityDeniesAFileEffect_ReportsUnavailableAndStagesNothing()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await CommitAsync(fixture, store);
        var request = fixture.CreatePrepare("other bytes"u8.ToArray(), idempotencyKey: "denied");
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        fixture.Authority.Deny = true;

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheReaderProfileIsUnavailableDuringRecovery_FailsClosedWithAnException()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        fixture.Selector.ReaderUnavailable = true;
        var request = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<IOException>(async () => await store.PrepareAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reopen_WhenTheLogEndsWithATornAppend_RepairsItAndKeepsEveryAcknowledgedEntry()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        _ = await Effects(fixture).WriteAsync(
            Authorization(fixture), FileSystemArtifactLayout.LogName, "{\"entries\":[{\"tenant\":\"torn"u8.ToArray(), FileWriteDisposition.Append, TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();

        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
        var log = Encoding.UTF8.GetString((await Effects(fixture).ReadAsync(Authorization(fixture), FileSystemArtifactLayout.LogName, 1_000_000, TestContext.Current.CancellationToken))!);
        log.ShouldNotContain("torn");
        log.EndsWith('\n').ShouldBeTrue();
    }

    [Fact]
    public async Task Reopen_WhenTheLogExceedsTheCompactionThreshold_RewritesOneSnapshotPerEntry()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var settings = new FileSystemArtifactSettings(1_048_576, 64L * 1_024 * 1_024, 64L * 1_024 * 1_024, 2, TimeSpan.FromMinutes(1));
        ArtifactReference kept;
        using (var store = fixture.NewStore(fixture.Target, settings))
        {
            kept = await CommitAsync(fixture, store);
            _ = await CommitAsync(fixture, store);
        }

        using var reopened = fixture.NewStore(fixture.Target, settings);
        (await ReadTextAsync(fixture, reopened, kept)).ShouldBe("durable bytes");

        var log = Encoding.UTF8.GetString((await Effects(fixture).ReadAsync(Authorization(fixture), FileSystemArtifactLayout.LogName, 1_000_000, TestContext.Current.CancellationToken))!);
        log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(2);
    }

    [Fact]
    public async Task ReadAsync_WhenAPayloadFileWasTamperedWith_ReportsUnavailableNeverTheAlteredContent()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        _ = await Effects(fixture).WriteAsync(
            Authorization(fixture), FileSystemArtifactLayout.PayloadName(reference.TenantId, reference.Integrity.ContentHash),
            "tampered"u8.ToArray(), FileWriteDisposition.ReplaceExisting, TestContext.Current.CancellationToken);
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);

        (await store.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheEntryExceedsTheConfiguredRecordBound_ReportsUnavailableAndLeavesNoEntry()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var settings = new FileSystemArtifactSettings(1_500, 64L * 1_024 * 1_024, 64L * 1_024 * 1_024, 4_096, TimeSpan.FromMinutes(1));
        using var store = fixture.NewStore(fixture.Target, settings);
        var declared = fixture.CreateMetadata(_content);
        var oversized = new ArtifactMetadata(
            declared.OwnerId, new string('m', 4_000), declared.DeclaredLength, declared.DeclaredContentHash, declared.Classification,
            declared.Ownership, declared.Mutability, declared.Retention, null);
        var request = fixture.CreatePrepare(_content, metadata: oversized);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        (await Effects(fixture).ReadAsync(Authorization(fixture), FileSystemArtifactLayout.LogName, 1_000_000, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task PrepareAsync_WhenObserved_LabelsTheAdapterAsFileSystem()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var unique = TestExecutionIdentity.Create(new TenantId($"tenant-{Guid.NewGuid():N}"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var request = fixture.CreatePrepare(_content, unique);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("file_system");
    }

    [Fact]
    public void Layout_WhenNamingPayloads_PartitionsByTenantAndRejectsUnsafeHashes()
    {
        var hash = FileSecurityBinding.ContentFingerprint("x"u8);

        FileSystemArtifactLayout.PayloadName(new TenantId("a"), hash).ShouldNotBe(FileSystemArtifactLayout.PayloadName(new TenantId("b"), hash));
        FileSystemArtifactLayout.PayloadName(new TenantId("a"), hash).ShouldBe(FileSystemArtifactLayout.PayloadName(new TenantId("a"), hash));
        FileSystemArtifactLayout.PayloadName(new TenantId("a"), hash).ShouldNotContain("/");
        _ = Should.Throw<InvalidDataException>(() => FileSystemArtifactLayout.PayloadName(new TenantId("a"), new ContentHash("sha256:../../etc")));
        Should.Throw<ArgumentException>(() => FileSystemArtifactLayout.PayloadName(default, hash)).ParamName.ShouldBe("tenantId");
    }

    private static FileSystemArtifactEffects Effects(FileSystemArtifactStoreConformanceFixture fixture) => new(
        fixture.Target, FileSystemArtifactSettings.CreateDefault(), fixture.Selector, new FixedSecurityAuthoritySelector(fixture.Authority),
        new SequentialRequestIds(), new FileIds(), fixture.ClockProvider);

    private static SecurityAuthorizationContext Authorization(FileSystemArtifactStoreConformanceFixture fixture)
    {
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-0000000000f4")), new RunId(Guid.Parse("30000000-0000-0000-0000-0000000000f3")), null);
        return TestSecurityEvidence.Authorization(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-0000000000f1")), new SessionId(Guid.Parse("20000000-0000-0000-0000-0000000000f2")),
            correlation, fixture.PrimaryIdentity);
    }

    private static async Task<ArtifactReference> CommitAsync(FileSystemArtifactStoreConformanceFixture fixture, IArtifactStore store)
    {
        var prepare = fixture.CreatePrepare(_content, idempotencyKey: $"commit-{Guid.NewGuid():N}");
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var prepared = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();
        var finalize = fixture.CreateFinalize(prepared.PreparationId, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        return (await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalized>().Reference;
    }

    private static async Task<ArtifactStoreDeleteResult> DeleteAsync(FileSystemArtifactStoreConformanceFixture fixture, IArtifactStore store, ArtifactReference reference)
    {
        var delete = fixture.CreateDelete(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);
        return await store.DeleteAsync(delete, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadTextAsync(FileSystemArtifactStoreConformanceFixture fixture, IArtifactStore store, ArtifactReference reference)
    {
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        await using var opened = (await store.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private sealed class SequentialRequestIds: IIdentifierGenerator<SecurityRequestId>
    {
        private long _next;

        public SecurityRequestId Create()
        {
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, Interlocked.Increment(ref _next));
            bytes[15] = 0x52;
            return new SecurityRequestId(new Guid(bytes));
        }
    }

    private sealed class FileIds: IIdentifierGenerator<FileOperationId>
    {
        private long _next;

        public FileOperationId Create()
        {
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, Interlocked.Increment(ref _next));
            bytes[15] = 0x53;
            return new FileOperationId(new Guid(bytes));
        }
    }
}
