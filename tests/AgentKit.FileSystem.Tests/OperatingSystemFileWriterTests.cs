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

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"agentkit-fs-root-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static IFileWriter CreateWriter(string root, ISecurityAuditDispatcher? audit = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(TestSecurity.GrantStore());
        _ = services.AddSingleton(audit ?? new AcceptingAuditDispatcher());
        _ = services.AddOperatingSystemFileSystem(
            new FileSystemProfileKey("test"),
            options => options.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root)));
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
