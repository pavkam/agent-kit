// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

public sealed class ReadFileToolTests
{
    [Fact]
    public void Constructor_WhenFileSystemNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReadFileTool(null!));

        exception.ParamName.ShouldBe("fileSystem");
    }

    [Fact]
    public void Descriptor_WhenAccessed_DeclaresReadOnlyEffect() =>
        new ReadFileTool(new FakeFileSystem()).Descriptor.Effect.ShouldBe(ToolEffect.ReadOnly);

    [Fact]
    public async Task InvokeAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => tool.InvokeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task InvokeAsync_WhenPathMissing_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request("{}"), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentsNotAnObject_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request("[]"), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenPathWhitespace_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "   "}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetExplicitlyNull_ReadsFullContent()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2", 5) };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": null}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldBe("l1\nl2");
    }

    [Fact]
    public async Task InvokeAsync_WhenPathContainsTraversal_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetNotAnInteger_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": "two"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetNotPositive_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 0}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitNotAnInteger_ReturnsFailed()
    {
        var tool = new ReadFileTool(new FakeFileSystem());

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "limit": "two"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenFileFound_ReturnsFullContent()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("line1\nline2\nline3", 17) };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldBe("line1\nline2\nline3");
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetAndLimitProvided_ReturnsRequestedLineRange()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\nl4\nl5", 14) };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 2, "limit": 2}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l2\nl3");
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitExceedsAvailableLines_ReturnsRemainingLines()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3", 8) };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 2, "limit": 10}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l2\nl3");
    }

    [Fact]
    public async Task InvokeAsync_WhenFileNotFound_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnRead = static r => new FileNotFound(r.Path) };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "missing.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemDenies_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileReadDenied("outside sandbox") };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("outside sandbox");
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemFails_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileReadFailed("disk error") };
        var tool = new ReadFileTool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenUsingRealSandboxedFileSystem_ReadsFileEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-readtool-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "doc.txt"), "real content");
            var fileSystem = new SandboxedFileSystem(Options.Create(new SandboxedFileSystemOptions { RootDirectory = root }));
            var tool = new ReadFileTool(fileSystem);

            var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "doc.txt"}"""), TestContext.Current.CancellationToken);

            result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
            TestFactory.ReadText(result).ShouldBe("real content");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
