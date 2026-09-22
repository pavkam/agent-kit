// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

public sealed class WriteFileToolTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    public async Task InvokeAsync_WhenContentIsEmptyOrWhitespace_WritesExactContent(string content)
    {
        var fileSystem = new FakeFileSystem { OnWrite = static request => new LegacyFileWritten(request.Content.Length) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(JsonSerializer.Serialize(new { path = "a.txt", content, mode = "create_or_replace" })),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        fileSystem.ReceivedWrites.ShouldHaveSingleItem().Content.ShouldBe(content);
    }

    [Fact]
    public void Constructor_WhenFileSystemNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WriteFileTool(
            null!, null!, null!, null!));

        exception.ParamName.ShouldBe("fileSystem");
    }

    [Fact]
    public void Descriptor_WhenAccessed_DeclaresAuthoredWriteContractAndOpenInputSchema()
    {
        var descriptor = TestFactory.Tool().Descriptor;

        descriptor.Effects.Effect.ShouldBe(ToolEffect.Mutating);
        descriptor.Version.ShouldBe(new ToolVersion("1.0"));
        descriptor.SourceId.ShouldBe(new ToolSourceId("agentkit.tools.write"));
        descriptor.InputSchema.Dialect.ShouldBe(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"));
        descriptor.InputSchema.Document.TryGetProperty("additionalProperties", out _).ShouldBeFalse();
        descriptor.InputSchema.Document.GetProperty("required").EnumerateArray()
            .Select(static value => value.GetString())
            .ShouldContain("mode");
        descriptor.InputSchema.Document.GetProperty("properties").GetProperty("mode").GetProperty("enum")
            .EnumerateArray().Select(static value => value.GetString())
            .ShouldContain("replace_existing");
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var tool = TestFactory.Tool();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => tool.InvokeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task InvokeAsync_WhenPathMissing_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenContentMissing_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentsNotAnObject_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request("[]"), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenPathWhitespace_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "   ", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenModeInvalid_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "delete"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenPathContainsTraversal_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenPathContainsTraversalWithValidMode_ReturnsInvalidPathMessageWithoutWriting()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new LegacyFileWritten(r.Content.Length) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt", "content": "hi", "mode": "create_or_replace"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.FailureReason.ShouldStartWith("Invalid path:");
        fileSystem.ReceivedWrites.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenModeOmitted_ReturnsRejectedWithoutWriting()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new LegacyFileWritten(r.Content.Length) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        fileSystem.ReceivedWrites.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("overwrite", FileWriteMode.CreateOrOverwrite)]
    [InlineData("create_or_replace", FileWriteMode.CreateOrOverwrite)]
    [InlineData("create_new", FileWriteMode.CreateNew)]
    [InlineData("create_only", FileWriteMode.CreateNew)]
    [InlineData("replace_existing", FileWriteMode.ReplaceExisting)]
    [InlineData("append", FileWriteMode.Append)]
    public async Task InvokeAsync_WhenModeSpecified_TranslatesToRequestedFileWriteMode(string mode, FileWriteMode expected)
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new LegacyFileWritten(r.Content.Length) };
        var tool = TestFactory.Tool(fileSystem);

        _ = await tool.InvokeAsync(
            TestFactory.Request($$"""{"path": "a.txt", "content": "hi", "mode": "{{mode}}"}"""), TestContext.Current.CancellationToken);

        fileSystem.ReceivedWrites.ShouldHaveSingleItem().Mode.ShouldBe(expected);
    }

    [Fact]
    public async Task InvokeAsync_WhenWriteSucceeds_ReturnsSuccessWithByteCount()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new LegacyFileWritten(42) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Succeeded);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        TestFactory.ReadText(result).ShouldContain("42");
    }

    [Fact]
    public async Task InvokeAsync_WhenFileAlreadyExists_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static r => new LegacyFileAlreadyExists(r.Path) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_new"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemFails_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new LegacyFileWriteFailed("disk error") };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemDenies_ReturnsRejected()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new LegacyFileWriteDenied("too large") };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("too large");
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityAuthorityDenies_DoesNotMutateFileSystem()
    {
        var fileSystem = new FakeFileSystem { OnWrite = static _ => new LegacyFileWritten(2) };
        var tool = TestFactory.Tool(fileSystem, TestFactory.DenyingAuthority());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("Denied by test policy.");
        fileSystem.ReceivedWrites.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenUsingRealSandboxedFileSystem_WritesFileEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-writetool-" + Guid.NewGuid().ToString("N"));
        try
        {
            _ = Directory.CreateDirectory(root);
            var fileSystem = new SandboxedFileSystem(
                Options.Create(new SandboxedFileSystemOptions { RootDirectory = root }),
                TestFactory.GrantStore(),
                TimeProvider.System);
            var tool = TestFactory.Tool(fileSystem);

            var result = await tool.InvokeAsync(
                TestFactory.Request(/*lang=json,strict*/ """{"path": "doc.txt", "content": "hello", "mode": "create_only"}"""), TestContext.Current.CancellationToken);

            result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
            File.ReadAllText(Path.Combine(root, "doc.txt")).ShouldBe("hello");
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
