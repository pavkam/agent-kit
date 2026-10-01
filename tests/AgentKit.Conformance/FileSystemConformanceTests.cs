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

    /// <summary>Verifies a file exactly at the authorized bound is exposed completely.</summary>
    [Fact]
    public async Task OpenReadAsync_WhenFileIsExactlyAtTheBound_ExposesAllContent()
    {
        await using var fixture = CreateFixture();
        var payload = new byte[64];
        await fixture.SeedFileAsync("exact.bin", payload, TestContext.Current.CancellationToken);

        var content = await ReadAllAsync(fixture, "exact.bin", maxBytes: 64);

        content.ShouldNotBeNull().Length.ShouldBe(64);
    }

    /// <summary>Verifies a missing file reports not-found rather than a failure or denial.</summary>
    [Fact]
    public async Task OpenReadAsync_WhenFileIsMissing_ReturnsNotFound()
    {
        await using var fixture = CreateFixture();

        var result = await fixture.Reader.OpenReadAsync(
            fixture.CreateAuthorizedRead("absent.txt", maxBytes: 64),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadOpenNotFound>();
    }

    /// <summary>Verifies create-only refuses an existing target and leaves its content untouched.</summary>
    [Fact]
    public async Task WriteAsync_WhenCreateOnlyAndTargetExists_ReturnsConflictWithoutMutation()
    {
        await using var fixture = CreateFixture();
        await fixture.SeedFileAsync("exists.txt", "original"u8.ToArray(), TestContext.Current.CancellationToken);

        var result = await WriteAsync(fixture, "exists.txt", "attempt", FileWriteDisposition.CreateOnly);

        _ = result.ShouldBeOfType<FileWriteConflict>();
        (await ReadTextAsync(fixture, "exists.txt")).ShouldBe("original");
    }

    /// <summary>Verifies append adds to the end of an existing target and reports the final size.</summary>
    [Fact]
    public async Task WriteAsync_WhenAppendAndTargetExists_AppendsContent()
    {
        await using var fixture = CreateFixture();
        await fixture.SeedFileAsync("log.txt", "one;"u8.ToArray(), TestContext.Current.CancellationToken);

        var result = await WriteAsync(fixture, "log.txt", "two;", FileWriteDisposition.Append);

        var success = result.ShouldBeOfType<FileWriteSuccess>();
        success.Outcome.ShouldBe(FileWriteOutcomeKind.Appended);
        success.PreviousBytes.ShouldBe(4);
        success.FinalBytes.ShouldBe(8);
        (await ReadTextAsync(fixture, "log.txt")).ShouldBe("one;two;");
    }

    /// <summary>Verifies append never creates a missing target.</summary>
    [Fact]
    public async Task WriteAsync_WhenAppendAndTargetIsMissing_ReturnsNotFoundWithoutCreating()
    {
        await using var fixture = CreateFixture();

        var result = await WriteAsync(fixture, "absent.log", "line", FileWriteDisposition.Append);

        _ = result.ShouldBeOfType<FileWriteNotFound>();
        (await ReadTextAsync(fixture, "absent.log")).ShouldBeNull();
    }

    /// <summary>Verifies replace-existing replaces exactly the target's content.</summary>
    [Fact]
    public async Task WriteAsync_WhenReplaceExistingAndTargetExists_ReplacesContent()
    {
        await using var fixture = CreateFixture();
        await fixture.SeedFileAsync("doc.txt", "old content"u8.ToArray(), TestContext.Current.CancellationToken);

        var result = await WriteAsync(fixture, "doc.txt", "new", FileWriteDisposition.ReplaceExisting);

        result.ShouldBeOfType<FileWriteSuccess>().Outcome.ShouldBe(FileWriteOutcomeKind.Replaced);
        (await ReadTextAsync(fixture, "doc.txt")).ShouldBe("new");
    }

    /// <summary>Verifies replace-existing never creates a missing target.</summary>
    [Fact]
    public async Task WriteAsync_WhenReplaceExistingAndTargetIsMissing_ReturnsNotFoundWithoutCreating()
    {
        await using var fixture = CreateFixture();

        var result = await WriteAsync(fixture, "absent.txt", "data", FileWriteDisposition.ReplaceExisting);

        _ = result.ShouldBeOfType<FileWriteNotFound>();
        (await ReadTextAsync(fixture, "absent.txt")).ShouldBeNull();
    }

    /// <summary>Verifies create-or-replace replaces an existing target and creates an absent one.</summary>
    [Fact]
    public async Task WriteAsync_WhenCreateOrReplace_CreatesAbsentAndReplacesExistingTargets()
    {
        await using var fixture = CreateFixture();

        var created = await WriteAsync(fixture, "either.txt", "first", FileWriteDisposition.CreateOrReplace);
        var replaced = await WriteAsync(fixture, "either.txt", "second", FileWriteDisposition.CreateOrReplace);

        created.ShouldBeOfType<FileWriteSuccess>().Outcome.ShouldBe(FileWriteOutcomeKind.Created);
        replaced.ShouldBeOfType<FileWriteSuccess>().Outcome.ShouldBe(FileWriteOutcomeKind.Replaced);
        (await ReadTextAsync(fixture, "either.txt")).ShouldBe("second");
    }

    /// <summary>Verifies a write whose parent directory is absent neither succeeds nor creates the parent.</summary>
    [Fact]
    public async Task WriteAsync_WhenParentDirectoryIsMissing_DoesNotCreateTheTarget()
    {
        await using var fixture = CreateFixture();

        var result = await WriteAsync(fixture, "missing/child.txt", "data", FileWriteDisposition.CreateOnly);

        result.ShouldNotBeOfType<FileWriteSuccess>();
        (await ReadTextAsync(fixture, "missing/child.txt")).ShouldBeNull();
    }

    /// <summary>Verifies concurrent create-only writers to one path produce exactly one creation.</summary>
    [Fact]
    public async Task WriteAsync_WhenCreateOnlyRacesOnTheSamePath_CreatesExactlyOnce()
    {
        await using var fixture = CreateFixture();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => Task.Run(
                async () => await WriteAsync(fixture, "race.txt", "data", FileWriteDisposition.CreateOnly),
                TestContext.Current.CancellationToken)));

        results.Count(static result => result is FileWriteSuccess).ShouldBe(1);
        results.Where(static result => result is not FileWriteSuccess).ShouldAllBe(static result => result is FileWriteConflict);
    }

    private static async ValueTask<FileWriteResult> WriteAsync(
        TFixture fixture,
        string path,
        string text,
        FileWriteDisposition disposition)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(text);
        return await fixture.Writer.WriteAsync(
            fixture.CreateAuthorizedWrite(path, payload, disposition),
            new FileWriteContent(payload, FileSecurityBinding.ContentFingerprint(payload)),
            TestContext.Current.CancellationToken);
    }

    private static async ValueTask<string?> ReadTextAsync(TFixture fixture, string path)
    {
        var bytes = await ReadAllAsync(fixture, path, maxBytes: 1024);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static async ValueTask<byte[]?> ReadAllAsync(TFixture fixture, string path, long maxBytes)
    {
        var result = await fixture.Reader.OpenReadAsync(
            fixture.CreateAuthorizedRead(path, maxBytes),
            TestContext.Current.CancellationToken);
        if (result is not FileReadHandleOpened opened)
        {
            return null;
        }

        await using var handle = opened.Handle;
        using var content = new MemoryStream();
        await handle.Content.CopyToAsync(content, TestContext.Current.CancellationToken);
        return content.ToArray();
    }
}
