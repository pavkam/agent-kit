// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

public sealed class SandboxedFileSystemEditTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentkit-edit-{Guid.NewGuid():N}");

    public SandboxedFileSystemEditTests() => _ = Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadSnapshotAsync_WhenSuccessful_ReturnsExactBytesHashAndEnforcement()
    {
        var bytes = new byte[] { 0xef, 0xbb, 0xbf, 0x61, 0x0d, 0x0a };
        await File.WriteAllBytesAsync(Path.Combine(_root, "a.txt"), bytes, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystem(grantStore);
        var request = new FileSnapshotRequest(new FileSystemPath("a.txt"), 100, TestSecurity.Grant());

        var result = await fileSystem.ReadSnapshotAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSnapshotStatus.Success);
        result.Content.ShouldBe(bytes);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(bytes));
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileRead);
        enforcement.Effect.ShouldBe(SecurityEffect.Observe);
        enforcement.Resources.ShouldBe([FileSecurityBinding.Resource(request.Path)]);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.SnapshotFingerprint(request.Path, 100));
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenGrantDenied_DoesNotRevealMissingTarget()
    {
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystem(grantStore);

        var result = await fileSystem.ReadSnapshotAsync(
            new FileSnapshotRequest(new FileSystemPath("missing"), 100, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionMatches_CommitsAtomicallyAndPreservesMode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var target = Path.Combine(_root, "script.sh");
        await File.WriteAllTextAsync(target, "old\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var original = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var replacement = "new\r\n"u8.ToArray().ToImmutableArray();
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystem(grantStore);
        var request = ReplaceRequest(
            "script.sh", FileSecurityBinding.ContentFingerprint(original), replacement, MutationId(1));

        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(replacement.AsSpan()));
        (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)).ShouldBe(replacement);
        File.GetUnixFileMode(target).ShouldBe(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Replace);
        enforcement.Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path));
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(
            request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionChanged_ReturnsConflictWithoutStaging()
    {
        var target = Path.Combine(_root, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();
        var request = ReplaceRequest(
            "a.txt", new ContentHash("sha256:stale"), "replacement"u8.ToArray().ToImmutableArray(), MutationId(2));

        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AtomicFileReplaceStatus.Conflict);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenGrantDenied_CreatesNoStagingFile()
    {
        var target = Path.Combine(_root, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystem(grantStore);

        var result = await fileSystem.ReplaceAsync(
            ReplaceRequest(
                "a.txt",
                FileSecurityBinding.ContentFingerprint(bytes),
                "replacement"u8.ToArray().ToImmutableArray(),
                MutationId(3)),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_root, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenTwoPlansRace_OnlyOneExpectedVersionCommits()
    {
        var target = Path.Combine(_root, "a.txt");
        await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);
        var expected = FileSecurityBinding.ContentFingerprint(
            await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        var fileSystem = CreateFileSystem();
        var first = fileSystem.ReplaceAsync(
            ReplaceRequest("a.txt", expected, "first"u8.ToArray().ToImmutableArray(), MutationId(4)),
            TestContext.Current.CancellationToken).AsTask();
        var second = fileSystem.ReplaceAsync(
            ReplaceRequest("a.txt", expected, "second"u8.ToArray().ToImmutableArray(), MutationId(5)),
            TestContext.Current.CancellationToken).AsTask();

        var results = await Task.WhenAll(first, second);

        results.Count(static result => result.Status == AtomicFileReplaceStatus.Committed).ShouldBe(1);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Conflict).ShouldBe(1);
        var content = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        (content is "first" or "second").ShouldBeTrue();
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private SandboxedFileSystem CreateFileSystem(ISecurityGrantStore? grantStore = null) => new(
        Options.Create(new SandboxedFileSystemOptions { RootDirectory = _root }),
        grantStore ?? TestSecurity.GrantStore(),
        TimeProvider.System);

    private static AtomicFileReplaceRequest ReplaceRequest(
        string path,
        ContentHash expected,
        ImmutableArray<byte> content,
        WorkspaceMutationId id) => new(id, new FileSystemPath(path), expected, content, TestSecurity.Grant());

    private static WorkspaceMutationId MutationId(int suffix) => new(Guid.Parse(
        $"10000000-0000-0000-0000-{suffix:D12}"));
}
