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
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer);

        var result = await tool.InvokeAsync(
            TestFactory.Request(JsonSerializer.Serialize(new { path = "a.txt", content, mode = "create_or_replace" })),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        writer.LastWrittenText.ShouldBe(content);
    }

    [Fact]

    public void Constructor_WhenFileSystemNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WriteFileTool(null!, null!, null!, null!, null!, null!));

        exception.ParamName.ShouldBe("fileSystemSelector");
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
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt", "content": "hi", "mode": "create_or_replace"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.FailureReason.ShouldNotBeNull().ShouldContain("traversal");
        writer.ReceivedWrites.ShouldBeEmpty();
    }

    [Fact]

    public async Task InvokeAsync_WhenModeOmitted_ReturnsRejectedWithoutWriting()
    {
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi"}"""), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        writer.ReceivedWrites.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("overwrite", FileWriteDisposition.CreateOrReplace)]
    [InlineData("create_or_replace", FileWriteDisposition.CreateOrReplace)]
    [InlineData("create_new", FileWriteDisposition.CreateOnly)]
    [InlineData("create_only", FileWriteDisposition.CreateOnly)]
    [InlineData("replace_existing", FileWriteDisposition.ReplaceExisting)]
    [InlineData("append", FileWriteDisposition.Append)]

    public async Task InvokeAsync_WhenModeSpecified_TranslatesToRequestedFileWriteMode(string mode, FileWriteDisposition expected)
    {
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer);

        _ = await tool.InvokeAsync(
            TestFactory.Request($$"""{"path": "a.txt", "content": "hi", "mode": "{{mode}}"}"""), TestContext.Current.CancellationToken);

        writer.ReceivedWrites.ShouldHaveSingleItem().Operation.Disposition.ShouldBe(expected);
    }

    [Fact]

    public async Task InvokeAsync_WhenWriteSucceeds_ReturnsSuccessWithByteCount()
    {
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Succeeded);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        TestFactory.ReadText(result).ShouldContain("2");
    }

    [Fact]

    public async Task InvokeAsync_WhenFileAlreadyExists_ReturnsFailed()
    {
        var writer = new FakeFileWriter { OnWrite = static (_, _) => new FileWriteConflict(new ResolvedFileTarget(new FileRootId("w"), new NormalizedRelativePath("a.txt"), "x", FilePathComparisonKind.Ordinal, FileSecurityBinding.ContentFingerprint("no-link"u8), FileSecurityBinding.ContentFingerprint("t"u8)), "exists") };
        var tool = TestFactory.Tool(writer);

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
        var writer = new FakeFileWriter { OnWrite = static (_, _) => new FileWriteFailed("disk error") };
        var tool = TestFactory.Tool(writer);

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
        var writer = new FakeFileWriter { OnWrite = static (_, _) => new FileWriteDenied("too large") };
        var tool = TestFactory.Tool(writer);

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
        var writer = new FakeFileWriter().WithSuccess();
        var tool = TestFactory.Tool(writer, TestFactory.DenyingAuthority());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "content": "hi", "mode": "create_or_replace"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("Denied by test policy.");
        writer.ReceivedWrites.ShouldBeEmpty();
    }

}
