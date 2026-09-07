// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

public sealed class SandboxedFileSystemPatchTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentkit-patch-{Guid.NewGuid():N}");

    public SandboxedFileSystemPatchTests() => _ = Directory.CreateDirectory(_root);

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateIsValid_CommitsAtomicallyWithExactSecurityBinding()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystem(store);
        var entry = new WorkspacePatchCreate(
            MutationId(1),
            new FileSystemPath("created.txt"),
            "hello\r\n"u8.ToArray().ToImmutableArray(),
            TestSecurity.Grant());

        var result = await fileSystem.ApplyPatchAsync(
            new WorkspacePatchRequest([entry]),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.AtomicCommitted);
        result.Entries.Single().Status.ShouldBe(WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllBytesAsync(
            Path.Combine(_root, "created.txt"),
            TestContext.Current.CancellationToken)).ShouldBe(entry.Content);
        File.GetUnixFileMode(Path.Combine(_root, "created.txt")).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();

        var enforcement = store.Enforcements.Single();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe(WorkspacePatchSecurityBinding.CreateResources(entry.Id, entry.Path));
        enforcement.InputFingerprint.ShouldBe(
            WorkspacePatchSecurityBinding.CreateFingerprint(entry.Id, entry.Path, entry.Content));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMixedPlanIsValid_CommitsInSourceOrderWithHonestVisibilityStatus()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        await WriteAsync("replace.sh", "old");
        await WriteAsync("delete.txt", "delete");
        await WriteAsync("move.txt", "move");
        File.SetUnixFileMode(
            Path.Combine(_root, "replace.sh"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var replaceExpected = await FingerprintAsync("replace.sh");
        var deleteExpected = await FingerprintAsync("delete.txt");
        var moveExpected = await FingerprintAsync("move.txt");
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystem(store);
        var create = new WorkspacePatchCreate(
            MutationId(2), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var replace = new WorkspacePatchReplace(
            MutationId(3),
            new FileSystemPath("replace.sh"),
            replaceExpected,
            "updated\n"u8.ToArray().ToImmutableArray(),
            TestSecurity.Grant());
        var delete = new WorkspacePatchDelete(
            MutationId(4), new FileSystemPath("delete.txt"), deleteExpected, TestSecurity.Grant());
        var move = new WorkspacePatchMove(
            MutationId(5),
            new FileSystemPath("move.txt"),
            new FileSystemPath("moved.txt"),
            moveExpected,
            TestSecurity.Grant());

        var result = await fileSystem.ApplyPatchAsync(
            new WorkspacePatchRequest([create, replace, delete, move]),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.CommittedWithNonAtomicVisibility);
        result.Entries.Select(static item => item.Status).ShouldAllBe(
            static status => status == WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllTextAsync(Path.Combine(_root, "new.txt"), TestContext.Current.CancellationToken))
            .ShouldBe("new");
        (await File.ReadAllTextAsync(Path.Combine(_root, "replace.sh"), TestContext.Current.CancellationToken))
            .ShouldBe("updated\n");
        File.GetUnixFileMode(Path.Combine(_root, "replace.sh")).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Exists(Path.Combine(_root, "delete.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "move.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_root, "moved.txt"), TestContext.Current.CancellationToken))
            .ShouldBe("move");
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();

        store.Enforcements.Select(static item => item.Effect).ShouldBe([
            SecurityEffect.Create,
            SecurityEffect.Replace,
            SecurityEffect.Delete,
            SecurityEffect.Move,
        ]);
        store.Enforcements[1].Resources.ShouldBe(
            FileSecurityBinding.AtomicReplaceResources(replace.Id, replace.Path));
        store.Enforcements[1].InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(
            replace.Id, replace.Path, replace.ExpectedContentFingerprint, replace.Content));
        store.Enforcements[2].Resources.ShouldBe(WorkspacePatchSecurityBinding.DeleteResources(delete.Path));
        store.Enforcements[3].Resources.ShouldBe(
            WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterPreconditionIsStale_RejectsWholePlanBeforeEffects()
    {
        await WriteAsync("existing.txt", "current");
        var fileSystem = CreateFileSystem(new RecordingGrantStore());
        var create = new WorkspacePatchCreate(
            MutationId(6), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var stale = new WorkspacePatchReplace(
            MutationId(7),
            new FileSystemPath("existing.txt"),
            new ContentHash("sha256:stale"),
            "changed"u8.ToArray().ToImmutableArray(),
            TestSecurity.Grant());

        var result = await fileSystem.ApplyPatchAsync(
            new WorkspacePatchRequest([create, stale]),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.Entries.Select(static item => item.Status).ShouldAllBe(
            static status => status == WorkspacePatchEntryStatus.Unchanged);
        File.Exists(Path.Combine(_root, "new.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_root, "existing.txt"), TestContext.Current.CancellationToken))
            .ShouldBe("current");
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterGrantIsDenied_ObservesAndMutatesNothing()
    {
        var store = new RecordingGrantStore(deniedIndex: 1);
        var fileSystem = CreateFileSystem(store);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(
                MutationId(8), new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchDelete(
                MutationId(9), new FileSystemPath("secret-missing.txt"), new ContentHash("sha256:any"), TestSecurity.Grant()));

        var result = await fileSystem.ApplyPatchAsync(
            new WorkspacePatchRequest(entries),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.Count.ShouldBe(2);
        File.Exists(Path.Combine(_root, "first.txt")).ShouldBeFalse();
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPathsOverlap_RejectsBeforeGrantConsumption()
    {
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystem(store);
        var path = new FileSystemPath("same.txt");
        var request = new WorkspacePatchRequest([
            new WorkspacePatchCreate(
                MutationId(10), path, "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchDelete(
                MutationId(11), path, new ContentHash("sha256:any"), TestSecurity.Grant()),
        ]);

        var result = await fileSystem.ApplyPatchAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.ShouldBeEmpty();
        File.Exists(Path.Combine(_root, "same.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateTargetExists_DoesNotReplaceIt()
    {
        await WriteAsync("existing.txt", "original");
        var fileSystem = CreateFileSystem(new RecordingGrantStore());
        var entry = new WorkspacePatchCreate(
            MutationId(12),
            new FileSystemPath("existing.txt"),
            "replacement"u8.ToArray().ToImmutableArray(),
            TestSecurity.Grant());

        var result = await fileSystem.ApplyPatchAsync(
            new WorkspacePatchRequest([entry]),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        (await File.ReadAllTextAsync(Path.Combine(_root, "existing.txt"), TestContext.Current.CancellationToken))
            .ShouldBe("original");
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private SandboxedFileSystem CreateFileSystem(ISecurityGrantStore store) => new(
        Options.Create(new SandboxedFileSystemOptions { RootDirectory = _root }),
        store,
        TimeProvider.System);

    private async Task WriteAsync(string path, string content) => await File.WriteAllTextAsync(
        Path.Combine(_root, path), content, TestContext.Current.CancellationToken);

    private async Task<ContentHash> FingerprintAsync(string path) => FileSecurityBinding.ContentFingerprint(
        await File.ReadAllBytesAsync(Path.Combine(_root, path), TestContext.Current.CancellationToken));

    private static WorkspaceMutationId MutationId(int suffix) => new(Guid.Parse(
        $"20000000-0000-0000-0000-{suffix:D12}"));

    private sealed class RecordingGrantStore(int? deniedIndex = null): ISecurityGrantStore
    {
        public List<SecurityEnforcementRequest> Enforcements { get; } = [];

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            Enforcements.Add(enforcement);
            var denied = Enforcements.Count - 1 == deniedIndex;
            return ValueTask.FromResult(new GrantConsumptionResult(
                denied ? GrantConsumptionStatus.Unknown : GrantConsumptionStatus.Consumed,
                0,
                denied ? "Denied by test store." : "Consumed by test store."));
        }

        public ValueTask<bool> RevokeAsync(
            GrantId grantId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
}
