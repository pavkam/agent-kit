// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies <see cref="OperatingSystemFileWriter"/> dispositions and host boundary behavior.</summary>
public sealed class OperatingSystemFileWriterTests
{
    [Fact]
    public async Task WriteAsync_WhenAuditUnavailable_DeniesBeforeMutation()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var writer = CreateWriter(root, audit: new RejectingAuditDispatcher());
        var payload = "hello"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "new.txt", payload, FileWriteDisposition.CreateOnly, writer);

        var result = await writer.WriteAsync(
            operation,
            CreateContent(payload),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(root, "new.txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(FileWriteDisposition.CreateOnly, false, typeof(FileWriteSuccess))]
    [InlineData(FileWriteDisposition.CreateOnly, true, typeof(FileWriteConflict))]
    [InlineData(FileWriteDisposition.ReplaceExisting, false, typeof(FileWriteNotFound))]
    [InlineData(FileWriteDisposition.ReplaceExisting, true, typeof(FileWriteSuccess))]
    [InlineData(FileWriteDisposition.Append, false, typeof(FileWriteNotFound))]
    [InlineData(FileWriteDisposition.Append, true, typeof(FileWriteSuccess))]
    public async Task WriteAsync_DispositionMatrix_ReturnsExpectedOutcome(
        FileWriteDisposition disposition,
        bool seedExisting,
        Type expectedOutcome)
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var relativePath = "target.bin";
        var hostPath = Path.Combine(root, relativePath);
        if (seedExisting)
        {
            await File.WriteAllTextAsync(hostPath, "seed", TestContext.Current.CancellationToken);
        }

        var writer = CreateWriter(root);
        var payload = "payload"u8.ToArray();
        ContentHash? expectedFingerprint = seedExisting
            ? FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(hostPath, TestContext.Current.CancellationToken))
            : null;
        var operation = CreateAuthorizedWrite(
            root,
            relativePath,
            payload,
            disposition,
            writer,
            expectedFingerprint);

        var result = await writer.WriteAsync(
            operation,
            CreateContent(payload),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType(expectedOutcome);
        if (expectedOutcome == typeof(FileWriteSuccess))
        {
            File.Exists(hostPath).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task WriteAsync_WhenExpectedFingerprintMismatch_ReturnsConflict()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var relativePath = "notes.txt";
        var hostPath = Path.Combine(root, relativePath);
        await File.WriteAllTextAsync(hostPath, "original", TestContext.Current.CancellationToken);
        var writer = CreateWriter(root);
        var payload = "updated"u8.ToArray();
        var operation = CreateAuthorizedWrite(
            root,
            relativePath,
            payload,
            FileWriteDisposition.ReplaceExisting,
            writer,
            expectedTargetFingerprint: new ContentHash("wrong"));

        var result = await writer.WriteAsync(
            operation,
            CreateContent(payload),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteConflict>();
        (await File.ReadAllTextAsync(hostPath, TestContext.Current.CancellationToken)).ShouldBe("original");
    }

    [Fact]
    public async Task WriteAsync_WhenTargetIsANamedPipe_FailsWithoutHanging()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = CreateTempRoot();
        await MakeFifoAsync(Path.Combine(root, "fifo.txt"));
        var writer = CreateWriter(root);
        var payload = "data"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "fifo.txt", payload, FileWriteDisposition.Append, writer);

        var task = writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken).AsTask();
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.ShouldBeSameAs(task, "WriteAsync must not block indefinitely opening a named pipe with no reader");
        (await task).ShouldNotBeOfType<FileWriteSuccess>();
    }

    [Fact]
    public async Task WriteAsync_WhenCreateOrReplaceTargetsAnExistingFile_ReplacesAtomicallyAndPreservesMode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = CreateTempRoot();
        var target = Path.Combine(root, "notes.txt");
        await File.WriteAllTextAsync(target, "old content", TestContext.Current.CancellationToken);
        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.SetUnixFileMode(target, mode);
        var writer = CreateWriter(root);
        var payload = "new"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "notes.txt", payload, FileWriteDisposition.CreateOrReplace, writer);

        var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

        var success = result.ShouldBeOfType<FileWriteSuccess>();
        success.Outcome.ShouldBe(FileWriteOutcomeKind.Replaced);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("new");
        File.GetUnixFileMode(target).ShouldBe(mode);
        Directory.GetFiles(root).Select(Path.GetFileName).ShouldBe(["notes.txt"], "no staging file may remain after a committed replace");
    }

    [Theory]
    [InlineData(FileWriteDisposition.CreateOnly)]
    [InlineData(FileWriteDisposition.CreateOrReplace)]
    [InlineData(FileWriteDisposition.ReplaceExisting)]
    [InlineData(FileWriteDisposition.Append)]
    public async Task WriteAsync_WhenTargetIsASymbolicLinkOutsideRoot_HasNoOutsideEffect(FileWriteDisposition disposition)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        try
        {
            var outsideFile = Path.Combine(outside, "victim.txt");
            await File.WriteAllTextAsync(outsideFile, "untouched", TestContext.Current.CancellationToken);
            _ = File.CreateSymbolicLink(Path.Combine(root, "link.txt"), outsideFile);
            var writer = CreateWriter(root);
            var payload = "attack"u8.ToArray();
            var operation = CreateAuthorizedWrite(root, "link.txt", payload, disposition, writer);

            var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

            result.ShouldNotBeOfType<FileWriteSuccess>();
            (await File.ReadAllTextAsync(outsideFile, TestContext.Current.CancellationToken)).ShouldBe("untouched");
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_WhenPathTraversesADirectorySymbolicLink_HasNoOutsideEffect()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(root, "escape"), outside);
            var writer = CreateWriter(root);
            var payload = "attack"u8.ToArray();
            var operation = CreateAuthorizedWrite(root, "escape/new.txt", payload, FileWriteDisposition.CreateOnly, writer);

            var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

            result.ShouldNotBeOfType<FileWriteSuccess>();
            File.Exists(Path.Combine(outside, "new.txt")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_WhenParentDirectoryIsMissing_DoesNotCreateTheParent()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var writer = CreateWriter(root);
        var payload = "data"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "missing/new.txt", payload, FileWriteDisposition.CreateOnly, writer);

        var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

        result.ShouldNotBeOfType<FileWriteSuccess>();
        Directory.Exists(Path.Combine(root, "missing")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenPayloadExceedsMaximumWriteBytes_ReturnsLimitExceededWithoutMutation()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var writer = CreateWriter(root, configure: options => options.Bounds = new FileSystemBounds(1024, 4));
        var payload = "too large"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "big.txt", payload, FileWriteDisposition.CreateOnly, writer);

        var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteLimitExceeded>();
        File.Exists(Path.Combine(root, "big.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenFingerprintPreconditionCannotBeVerifiedBecauseTheTargetIsTooLarge_FailsWithoutMutation()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var target = Path.Combine(root, "large.bin");
        await File.WriteAllBytesAsync(target, new byte[32], TestContext.Current.CancellationToken);
        var writer = CreateWriter(root, configure: options => options.Bounds = new FileSystemBounds(8, 1024));
        var payload = "new"u8.ToArray();
        var operation = CreateAuthorizedWrite(
            root, "large.bin", payload, FileWriteDisposition.ReplaceExisting, writer, FileSecurityBinding.ContentFingerprint(new byte[32]));

        var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteFailed>();
        (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)).Length.ShouldBe(32);
    }

    [Fact]
    public async Task WriteAsync_WhenCreateOnlyRacesOnTheSamePath_CreatesExactlyOnce()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var writer = CreateWriter(root);
        var payload = "data"u8.ToArray();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
            async () => await writer.WriteAsync(
                CreateAuthorizedWrite(root, "race.txt", payload, FileWriteDisposition.CreateOnly, writer),
                CreateContent(payload),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        results.Count(static result => result is FileWriteSuccess).ShouldBe(1);
        results.Where(static result => result is not FileWriteSuccess).ShouldAllBe(static result => result is FileWriteConflict);
    }

    [Fact]
    public async Task WriteAsync_WhenGrantIsDenied_DoesNotMutateAndBindsTheWriterAudience()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 1, "Grant does not match.", null),
        };
        var writer = CreateWriter(root, grantStore: store);
        var payload = "data"u8.ToArray();
        var operation = CreateAuthorizedWrite(root, "denied.txt", payload, FileWriteDisposition.CreateOnly, writer);

        var result = await writer.WriteAsync(operation, CreateContent(payload), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(root, "denied.txt")).ShouldBeFalse();
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Audience.ShouldBe(writer.SecurityAudience);
    }

    private static async Task MakeFifoAsync(string path)
    {
        using var mkfifo = Process.Start("mkfifo", path)
            ?? throw new InvalidOperationException("mkfifo could not be started.");
        await mkfifo.WaitForExitAsync(TestContext.Current.CancellationToken);
        mkfifo.ExitCode.ShouldBe(0);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"agentkit-fs-root-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static IFileWriter CreateWriter(
        string root,
        ISecurityAuditDispatcher? audit = null,
        ISecurityGrantStore? grantStore = null,
        Action<OperatingSystemFileSystemOptions>? configure = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(grantStore ?? TestSecurity.GrantStore());
        _ = services.AddSingleton(audit ?? new AcceptingAuditDispatcher());
        _ = services.AddOperatingSystemFileSystem(
            new FileSystemProfileKey("test"),
            options =>
            {
                options.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root));
                configure?.Invoke(options);
            });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<IFileWriter>("test");
    }

    private static AuthorizedFileWrite CreateAuthorizedWrite(
        string root,
        string relativePath,
        byte[] payload,
        FileWriteDisposition disposition,
        IFileWriter writer,
        ContentHash? expectedTargetFingerprint = null)
    {
        _ = writer;
        var hostTarget = Path.GetFullPath(Path.Combine(root, relativePath));
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload);
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTarget,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            expectedTargetFingerprint ?? FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedFileWrite(
            target,
            disposition,
            expectedTargetFingerprint,
            payload.Length,
            payloadFingerprint,
            FileWriteAtomicityMode.Required,
            FileWriteEffectClass.WorkspaceBytes,
            TestSecurity.Grant());
    }

    private static FileWriteContent CreateContent(byte[] payload)
    {
        var fingerprint = FileSecurityBinding.ContentFingerprint(payload);
        return new FileWriteContent(payload, fingerprint);
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private sealed class RejectingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditUnavailable("No sink."));
    }
}
