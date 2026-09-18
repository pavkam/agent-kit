// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

public sealed class ReadFileToolTests
{
    [Fact]
    public void Constructor_WhenFileSystemNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReadFileTool(
            null!, null!, null!, null!, null!));

        exception.ParamName.ShouldBe("fileSystem");
    }

    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReadFileTool(
            new FakeFileSystem(),
            TestFactory.DenyingAuthority(),
            TestFactory.RequestIds(),
            TimeProvider.System,
            null!));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(11, 10)]
    public void Constructor_WhenOptionsInvalid_ThrowsArgumentOutOfRangeException(int defaultMaximumLines, int maximumLines)
    {
        var options = new ReadFileToolOptions { DefaultMaximumLines = defaultMaximumLines, MaximumLines = maximumLines };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => TestFactory.Tool(options: options));
    }

    [Fact]
    public void Constructor_WhenDefaultEqualsMaximum_Succeeds()
    {
        var options = new ReadFileToolOptions { DefaultMaximumLines = 10, MaximumLines = 10 };

        var tool = TestFactory.Tool(options: options);

        tool.Descriptor.Id.ShouldBe(ReadFileTool.Id);
    }

    [Fact]
    public void Descriptor_WhenAccessed_DeclaresAuthoredReadContractAndOpenInputSchema()
    {
        var descriptor = TestFactory.Tool().Descriptor;

        descriptor.Effects.Effect.ShouldBe(ToolEffect.ReadOnly);
        descriptor.Version.ShouldBe(new ToolVersion("1.0"));
        descriptor.SourceId.ShouldBe(new ToolSourceId("agentkit.tools.read"));
        descriptor.InputSchema.Dialect.ShouldBe(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"));
        descriptor.InputSchema.Document.TryGetProperty("additionalProperties", out _).ShouldBeFalse();
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

        var result = await tool.InvokeAsync(TestFactory.Request("{}"), TestContext.Current.CancellationToken);

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

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "   "}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetExplicitlyNull_ReadsFullContent()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2", 5) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": null}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldBe("l1\nl2");
    }

    [Fact]
    public async Task InvokeAsync_WhenPathContainsTraversal_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "../escape.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetNotAnInteger_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": "two"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetNotPositive_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 0}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitNotAnInteger_ReturnsRejected()
    {
        var tool = TestFactory.Tool();

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "limit": "two"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileFound_ReturnsFullContent()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("line1\nline2\nline3", 17) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Succeeded);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        TestFactory.ReadText(result).ShouldBe("line1\nline2\nline3");
    }

    [Fact]
    public async Task InvokeAsync_WhenOffsetAndLimitProvided_ReturnsRequestedLineRange()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\nl4\nl5", 14) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 2, "limit": 2}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l2\nl3");
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitExceedsAvailableLines_ReturnsRemainingLines()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3", 8) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 2, "limit": 10}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l2\nl3");
        TestFactory.ReadComplete(result).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitOmitted_UsesConfiguredDefaultWindow()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\nl4\nl5", 14) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 10 });

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldBe("l1\nl2");
        TestFactory.ReadComplete(result).ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitOmittedAndOffsetProvided_UsesConfiguredDefaultWindowFromOffset()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\nl4\nl5", 14) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 10 });

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 3}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l3\nl4");
        TestFactory.ReadComplete(result).ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitOmittedAndFileFitsWindow_ReturnsFullContentMarkedComplete()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\r\nl2", 6) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 10 });

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l1\r\nl2");
        TestFactory.ReadComplete(result).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileHasATrailingNewlineAndFitsWindow_ReturnsFullContentMarkedComplete()
    {
        // Split('\n') turns a trailing line terminator into one extra, phantom empty final element ("l1\nl2\n"
        // splits into ["l1", "l2", ""]). Counting that element as a real logical line made a read that already
        // reached the file's true end report complete: false, so a follow-up read at the next offset would
        // return nothing instead of the caller ever observing a complete: true window.
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\n", 6) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 10 });

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l1\nl2\n");
        TestFactory.ReadComplete(result).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileHasATrailingNewlineAndAnExplicitRangeReachesTheEnd_MarksComplete()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\n", 9) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 10 });

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "offset": 3, "limit": 5}"""), TestContext.Current.CancellationToken);

        TestFactory.ReadText(result).ShouldBe("l3");
        TestFactory.ReadComplete(result).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitWithinMaximum_ReturnsRequestedLines()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3\nl4\nl5", 14) };
        var tool = TestFactory.Tool(fileSystem, options: new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 4 });

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "limit": 4}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        TestFactory.ReadText(result).ShouldBe("l1\nl2\nl3\nl4");
        TestFactory.ReadComplete(result).ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitExceedsMaximum_ReturnsInvalidArguments()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("l1\nl2\nl3", 8) };
        var tool = TestFactory.Tool(
            fileSystem, new TestSupport.UninvokedSecurityAuthority(), new ReadFileToolOptions { DefaultMaximumLines = 2, MaximumLines = 4 });

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt", "limit": 5}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("Property 'limit' must be between 1 and 4.");
        fileSystem.ReceivedReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileNotFound_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnRead = static r => new FileNotFound(r.Path) };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "missing.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemDenies_ReturnsRejected()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileReadDenied("outside sandbox") };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("outside sandbox");
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityAuthorityDenies_DoesNotObserveFileSystem()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileRead("secret", 6) };
        var tool = TestFactory.Tool(fileSystem, TestFactory.DenyingAuthority());

        var result = await tool.InvokeAsync(
            TestFactory.Request(/*lang=json,strict*/ """{"path": "missing-or-secret.txt"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("Denied by test policy.");
        fileSystem.ReceivedReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenFileSystemFails_ReturnsFailed()
    {
        var fileSystem = new FakeFileSystem { OnRead = static _ => new FileReadFailed("disk error") };
        var tool = TestFactory.Tool(fileSystem);

        var result = await tool.InvokeAsync(TestFactory.Request(/*lang=json,strict*/ """{"path": "a.txt"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        result.Outcome.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenUsingRealSandboxedFileSystem_ReadsFileEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-readtool-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "doc.txt"), "real content");
            var fileSystem = new SandboxedFileSystem(
                Options.Create(new SandboxedFileSystemOptions { RootDirectory = root }),
                TestFactory.GrantStore(),
                TimeProvider.System);
            var tool = TestFactory.Tool(fileSystem);

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
