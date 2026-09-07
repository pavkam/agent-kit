// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

public sealed class SandboxedFileSystemTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentkit-fs-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outsideRoot = Path.Combine(Path.GetTempPath(), "agentkit-fs-outside-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        var outsideLink = Path.Combine(_root, "outside-link");
        if (Directory.Exists(outsideLink) && new DirectoryInfo(outsideLink).LinkTarget is not null)
        {
            Directory.Delete(outsideLink);
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_outsideRoot))
        {
            Directory.Delete(_outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SandboxedFileSystem(null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenRootDirectoryNotRooted_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "relative/path" });

        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(options));
    }

    [Fact]
    public void Constructor_WhenRootDirectoryBlank_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "   " });

        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(options));
    }

    [Fact]
    public async Task ReadAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var fs = CreateFileSystem();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => fs.ReadAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var fs = CreateFileSystem();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => fs.WriteAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenFileDoesNotExist_CreatesFileAndReturnsWritten()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("notes.txt"), "hello world", FileWriteMode.CreateOrOverwrite);

        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);

        var written = result.ShouldBeOfType<FileWritten>();
        written.BytesWritten.ShouldBe(11L);
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("hello world");
    }

    [Fact]
    public async Task WriteAsync_WhenNestedPath_CreatesParentDirectories()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("sub/dir/notes.txt"), "nested", FileWriteMode.CreateOrOverwrite);

        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, "sub", "dir", "notes.txt")).ShouldBe("nested");
    }

    [Fact]
    public async Task ReadAsync_WhenFileExists_ReturnsContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "existing content");

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt")), TestContext.Current.CancellationToken);

        var read = result.ShouldBeOfType<FileRead>();
        read.Content.ShouldBe("existing content");
        read.Bytes.ShouldBe(16L);
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExist_ReturnsFileNotFound()
    {
        var fs = CreateFileSystem();

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("missing.txt")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task ReadAsync_WhenPathTraversesDirectorySymbolicLink_ReturnsFileReadDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        File.WriteAllText(Path.Combine(_outsideRoot, "secret.txt"), "outside");
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside-link"), _outsideRoot);

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("outside-link/secret.txt")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadDenied>();
    }

    [Fact]
    public async Task ReadAsync_WhenTargetIsSymbolicLinkOutsideRoot_ReturnsFileReadDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        var outsidePath = Path.Combine(_outsideRoot, "secret.txt");
        File.WriteAllText(outsidePath, "outside");
        _ = File.CreateSymbolicLink(Path.Combine(_root, "secret-link.txt"), outsidePath);

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("secret-link.txt")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadDenied>();
    }

    [Fact]
    public async Task WriteAsync_WhenPathTraversesDirectorySymbolicLink_ReturnsFileWriteDeniedWithoutOutsideEffect()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside-link"), _outsideRoot);

        var result = await fs.WriteAsync(
            new FileWriteRequest(
                new FileSystemPath("outside-link/created.txt"),
                "must stay inside",
                FileWriteMode.CreateOrOverwrite),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(_outsideRoot, "created.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenTargetIsSymbolicLinkOutsideRoot_ReturnsFileWriteDeniedWithoutOutsideEffect()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        var outsidePath = Path.Combine(_outsideRoot, "secret.txt");
        File.WriteAllText(outsidePath, "outside");
        _ = File.CreateSymbolicLink(Path.Combine(_root, "secret-link.txt"), outsidePath);

        var result = await fs.WriteAsync(
            new FileWriteRequest(
                new FileSystemPath("secret-link.txt"),
                "must stay inside",
                FileWriteMode.CreateOrOverwrite),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.ReadAllText(outsidePath).ShouldBe("outside");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateNewAndFileExists_ReturnsFileAlreadyExists()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "already here");

        var result = await fs.WriteAsync(
            new FileWriteRequest(new FileSystemPath("notes.txt"), "new content", FileWriteMode.CreateNew),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileAlreadyExists>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("already here");
    }

    [Fact]
    public async Task WriteAsync_WhenConcurrentCreateNewTargetsSamePath_CreatesExactlyOnce()
    {
        var fs = CreateFileSystem();
        var first = new FileWriteRequest(new FileSystemPath("notes.txt"), "first", FileWriteMode.CreateNew);
        var second = new FileWriteRequest(new FileSystemPath("notes.txt"), "second", FileWriteMode.CreateNew);

        var results = await Task.WhenAll(
            fs.WriteAsync(first, TestContext.Current.CancellationToken),
            fs.WriteAsync(second, TestContext.Current.CancellationToken));

        results.Count(static result => result is FileWritten).ShouldBe(1);
        results.Count(static result => result is FileAlreadyExists).ShouldBe(1);
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBeOneOf("first", "second");
    }

    [Fact]
    public async Task WriteAsync_WhenRequestWasMutatedToUndefinedMode_ThrowsBeforeEffects()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(
            new FileSystemPath("notes.txt"),
            "content",
            FileWriteMode.CreateOrOverwrite) with
        {
            Mode = (FileWriteMode) int.MaxValue
        };

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => fs.WriteAsync(request, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request.Mode");
        File.Exists(Path.Combine(_root, "notes.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenModeAppend_AppendsToExistingContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "first-");

        var result = await fs.WriteAsync(
            new FileWriteRequest(new FileSystemPath("notes.txt"), "second", FileWriteMode.Append),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("first-second");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateOrOverwrite_ReplacesExistingContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "old content that is longer");

        var result = await fs.WriteAsync(
            new FileWriteRequest(new FileSystemPath("notes.txt"), "new", FileWriteMode.CreateOrOverwrite),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("new");
    }

    [Fact]
    public async Task ReadAsync_WhenFileExceedsMaximumReadBytes_ReturnsFileReadDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumReadBytes = 4);
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "this is too long");

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadDenied>();
    }

    [Fact]
    public async Task WriteAsync_WhenContentExceedsMaximumWriteBytes_ReturnsFileWriteDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 4);

        var result = await fs.WriteAsync(
            new FileWriteRequest(new FileSystemPath("notes.txt"), "this is too long", FileWriteMode.CreateOrOverwrite),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(_root, "notes.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenRootDirectoryDoesNotExist_ReturnsFileNotFound()
    {
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        var fs = new SandboxedFileSystem(Options.Create(options));

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task ReadAsync_WhenPathIsSingleDotSegment_ReturnsFileReadFailed()
    {
        var fs = CreateFileSystem();

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath(".")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadFailed>();
    }

    private SandboxedFileSystem CreateFileSystem(Action<SandboxedFileSystemOptions>? configure = null)
    {
        _ = Directory.CreateDirectory(_root);
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        configure?.Invoke(options);
        return new SandboxedFileSystem(Options.Create(options));
    }
}
