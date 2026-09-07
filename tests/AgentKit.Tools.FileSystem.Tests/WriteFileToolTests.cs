// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.FileSystem.Tests;

public sealed class WriteFileToolTests
{
    [Fact]
    public void Constructor_WhenFileSystemNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WriteFileTool(null!));

        exception.ParamName.ShouldBe("fileSystem");
    }

    [Fact]
    public void Descriptor_WhenAccessed_DeclaresMutatingEffect() =>
        new WriteFileTool(new FakeFileSystem()).Descriptor.Effect.ShouldBe(ToolEffect.Mutating);

    [Fact]
    public async Task InvokeAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var tool = new WriteFileTool(new FakeFileSystem());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => tool.InvokeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task InvokeAsync_WhenPathMissing_ReturnsFailed()
    {
        var tool = new WriteFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenContentMissing_ReturnsFailed()
    {
        var tool = new WriteFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenModeInvalid_ReturnsFailed()
    {
        var tool = new WriteFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "delete"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenPathContainsTraversal_ReturnsFailed()
    {
        var tool = new WriteFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenModeOmitted_DefaultsToOverwrite()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new FileWritten(r.Content.Length) };
        var tool = new WriteFileTool(fileSystem);

        _ = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        fileSystem.ReceivedWrites.ShouldHaveSingleItem().Mode.ShouldBe(FileWriteMode.CreateOrOverwrite);
    }

    [Theory]
    [InlineData("overwrite", FileWriteMode.CreateOrOverwrite)]
    [InlineData("create_new", FileWriteMode.CreateNew)]
    [InlineData("append", FileWriteMode.Append)]
    public async Task InvokeAsync_WhenModeSpecified_TranslatesToRequestedFileWriteMode(string mode, FileWriteMode expected)
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new FileWritten(r.Content.Length) };
        var tool = new WriteFileTool(fileSystem);

        _ = await tool.InvokeAsync(
            TestFactory.Request($$"""{"path": "a.txt", "content": "hi", "mode": "{{mode}}"}"""), TestContext.Current.CancellationToken);

        fileSystem.ReceivedWrites.ShouldHaveSingleItem().Mode.ShouldBe(expected);
    }

    [Fact]
    public async Task InvokeAsync_WhenWriteSucceeds_ReturnsSuccessWithByteCount()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new FileWritten(42) };
        var tool = new WriteFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldContain("42");
    }

    [Fact]
    public async Task InvokeAsync_WhenFileAlreadyExists_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new FileAlreadyExists(r.Path) };
        var tool = new WriteFileTool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_new"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemDenies_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new FileWriteDenied("too large") };
        var tool = new WriteFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("too large");
    }

    [Fact]
    public async Task InvokeAsync_WhenUsingRealSandboxedFileSystem_WritesFileEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-writetool-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fileSystem = new SandboxedFileSystem(Options.Create(new SandboxedFileSystemOptions { RootDirectory = root }));
            var tool = new WriteFileTool(fileSystem);

            var result = await tool.InvokeAsync(
                TestFactory.Request(/*lang=json,strict*/ """{"path": "sub/doc.txt", "content": "hello"}"""), TestContext.Current.CancellationToken);

            result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
            File.ReadAllText(Path.Combine(root, "sub", "doc.txt")).ShouldBe("hello");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
