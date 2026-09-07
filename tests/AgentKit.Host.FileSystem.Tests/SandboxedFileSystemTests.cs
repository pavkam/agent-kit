// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Host.FileSystem.Tests;

public sealed class SandboxedFileSystemTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentkit-fs-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
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

    private SandboxedFileSystem CreateFileSystem(Action<SandboxedFileSystemOptions>? configure = null)
    {
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        configure?.Invoke(options);
        return new SandboxedFileSystem(Options.Create(options));
    }
}
