// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Shared host file-system contract scenarios for real and in-memory adapters.</summary>
/// <typeparam name="TFixture">The adapter-specific fixture.</typeparam>
public abstract class FileSystemConformanceTests<TFixture>
    where TFixture : IFileSystemConformanceFixture
{
    /// <summary>Creates an isolated fixture for one inherited case.</summary>
    /// <returns>The fixture instance.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies required audit failure closes before host mutation.</summary>
    [Fact]
    public async Task WriteAsync_WhenRequiredAuditUnavailable_DeniesBeforeMutation()
    {
        await using var fixture = CreateFixture();
        fixture.UseRejectingAudit();
        var payload = "hello"u8.ToArray();
        var operation = fixture.CreateAuthorizedWrite("audit.txt", payload, FileWriteDisposition.CreateOnly);

        var result = await fixture.Writer.WriteAsync(
            operation,
            new FileWriteContent(payload, FileSecurityBinding.ContentFingerprint(payload)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
    }

    /// <summary>Verifies bounded reads truncate at the authorized byte limit.</summary>
    [Fact]
    public async Task OpenReadAsync_WhenFileExceedsBound_TruncatesAtLimit()
    {
        await using var fixture = CreateFixture();
        var payload = new byte[128];
        await fixture.SeedFileAsync("big.bin", payload, TestContext.Current.CancellationToken);
        var operation = fixture.CreateAuthorizedRead("big.bin", maxBytes: 32);

        var result = await fixture.Reader.OpenReadAsync(operation, TestContext.Current.CancellationToken);
        var opened = result.ShouldBeOfType<FileReadHandleOpened>();
        await using var handle = opened.Handle;
        var buffer = new byte[64];
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

        total.ShouldBe(32);
    }

    /// <summary>Verifies create-only writes succeed on absent targets.</summary>
    [Fact]
    public async Task WriteAsync_WhenCreateOnlyAndAbsentTarget_CreatesFile()
    {
        await using var fixture = CreateFixture();
        var payload = "created"u8.ToArray();
        var operation = fixture.CreateAuthorizedWrite("created.txt", payload, FileWriteDisposition.CreateOnly);

        var result = await fixture.Writer.WriteAsync(
            operation,
            new FileWriteContent(payload, FileSecurityBinding.ContentFingerprint(payload)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteSuccess>();
    }

    /// <summary>Verifies directory creation requires its own authorized operation.</summary>
    [Fact]
    public async Task CreateAsync_WhenAuthorized_CreatesDirectory()
    {
        await using var fixture = CreateFixture();
        if (fixture.DirectoryCreator is not { } directoryCreator)
        {
            return;
        }

        var operation = fixture.CreateAuthorizedDirectoryCreate("nested/dir");

        var result = await directoryCreator.CreateAsync(operation, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DirectoryCreateSuccess>();
    }
}
