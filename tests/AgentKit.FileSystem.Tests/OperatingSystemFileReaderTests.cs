// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies <see cref="OperatingSystemFileReader"/> bounded reads and host boundary behavior.</summary>
public sealed class OperatingSystemFileReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenAuditUnavailable_DeniesBeforeOpening()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var filePath = Path.Combine(root, "notes.txt");
        await File.WriteAllTextAsync(filePath, "hello", TestContext.Current.CancellationToken);
        var reader = CreateReader(root, audit: new RejectingAuditDispatcher());
        var operation = CreateAuthorizedRead(root, "notes.txt", maxBytes: 100, reader);

        var result = await reader.OpenReadAsync(operation, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadOpenDenied>();
    }

    [Fact]
    public async Task OpenReadAsync_WhenFileExceedsBound_TruncatesAtAuthorizedLimit()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var filePath = Path.Combine(root, "big.bin");
        await File.WriteAllBytesAsync(filePath, new byte[200], TestContext.Current.CancellationToken);
        var reader = CreateReader(root);
        var operation = CreateAuthorizedRead(root, "big.bin", maxBytes: 64, reader);

        var result = await reader.OpenReadAsync(operation, TestContext.Current.CancellationToken);
        var opened = result.ShouldBeOfType<FileReadHandleOpened>();
        await using var handle = opened.Handle;
        var buffer = new byte[128];
        var total = 0;
        while (true)
        {
            var read = await handle.Content.ReadAsync(buffer.AsMemory(total), TestContext.Current.CancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        total.ShouldBe(64);
    }

    [Fact]
    public async Task OpenReadAsync_WhenSymlinkEscapesRoot_Denies()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var root = CreateTempRoot();
        var secretDir = Path.Combine(Path.GetTempPath(), $"agentkit-fs-secret-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(secretDir);
        try
        {
            var secret = Path.Combine(secretDir, "secret.txt");
            await File.WriteAllTextAsync(secret, "secret", TestContext.Current.CancellationToken);
            var linkPath = Path.Combine(root, "link.txt");
            _ = File.CreateSymbolicLink(linkPath, secret);
            var reader = CreateReader(root);
            var hostTarget = Path.GetFullPath(linkPath);
            var operation = CreateAuthorizedReadForHostPath(root, "link.txt", hostTarget, maxBytes: 32, reader);

            var result = await reader.OpenReadAsync(operation, TestContext.Current.CancellationToken);

            _ = result.ShouldBeOfType<FileReadOpenDenied>();
        }
        finally
        {
            Directory.Delete(secretDir, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"agentkit-fs-root-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    [Obsolete]
    private static IFileReader CreateReader(string root, ISecurityAuditDispatcher? audit = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TestSecurity.GrantStore());
        _ = services.AddSingleton(audit ?? new AcceptingAuditDispatcher());
        _ = services.AddOperatingSystemFileSystem(
            new FileSystemProfileKey("test"),
            options => options.Roots.Add(new FileRootRegistration(new FileRootId("workspace"), root)));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<IFileReader>("test");
    }

    private static AuthorizedFileRead CreateAuthorizedRead(
        string root,
        string relativePath,
        long maxBytes,
        IFileReader reader)
    {
        var hostTarget = Path.GetFullPath(Path.Combine(root, relativePath));
        return CreateAuthorizedReadForHostPath(root, relativePath, hostTarget, maxBytes, reader);
    }

    private static AuthorizedFileRead CreateAuthorizedReadForHostPath(
        string root,
        string relativePath,
        string hostTarget,
        long maxBytes,
        IFileReader reader)
    {
        _ = root;
        _ = reader;
        var target = new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath(relativePath));
        var request = new FileReadRequest(
            new FileOperationId(Guid.NewGuid()),
            new OperationId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            runId: null,
            target,
            new FileReadBounds(maxBytes));
        var resolved = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relativePath),
            hostTarget,
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        return new AuthorizedFileRead(request, resolved, TestSecurity.Grant());
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
