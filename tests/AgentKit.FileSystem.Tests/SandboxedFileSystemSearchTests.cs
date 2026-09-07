// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

public sealed class SandboxedFileSystemSearchTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentkit-search-{Guid.NewGuid():N}");

    public SandboxedFileSystemSearchTests() => _ = Directory.CreateDirectory(_root);

    [Fact]
    public async Task SearchAsync_WhenLiteralMatches_ReturnsDeterministicVersionedByteLocations()
    {
        _ = Directory.CreateDirectory(Path.Combine(_root, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "src", "b.cs"), "first\nNeedle café\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "src", "a.cs"), "needle\n", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();

        var result = await fileSystem.SearchAsync(
            Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), caseSensitive: false),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        result.VisitedFiles.ShouldBe(2);
        result.Matches.Select(static match => match.Path.Value).ShouldBe(["src/a.cs", "src/b.cs"]);
        result.Matches[1].LineNumber.ShouldBe(2);
        result.Matches[1].LineByteOffset.ShouldBe(6);
        result.Matches[1].MatchByteOffset.ShouldBe(0);
        result.Matches[1].MatchByteLength.ShouldBe(6);
        result.Matches.ShouldAllBe(static match => match.ContentFingerprint.Value.StartsWith("sha256:"));
    }

    [Fact]
    public async Task SearchAsync_WhenRegexAndPathFilterProvided_UsesPinnedEngines()
    {
        _ = Directory.CreateDirectory(Path.Combine(_root, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(_root, "src", "code.cs"), "item-42 item-x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "src", "notes.md"), "item-99", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();

        var result = await fileSystem.SearchAsync(
            Request(
                new FileSearchPattern(/*lang=regex*/ "item-[0-9]+", FileSearchPatternKind.RegularExpression),
                pathPattern: new GlobPattern("**/*.cs")),
            TestContext.Current.CancellationToken);

        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/code.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenFilesAreHiddenBinaryOrInvalidUtf8_ExcludesThemWithoutDecoding()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, ".hidden"), "needle", TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "binary"), "needle\0tail"u8.ToArray(), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "invalid"),
            [0x6e, 0x65, 0x65, 0x64, 0x6c, 0x65, 0xff],
            TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();

        var result = await fileSystem.SearchAsync(
            Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
        result.VisitedFiles.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchLimitReached_ReturnsTypedPartialResult()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "a.txt"), "needle needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();

        var result = await fileSystem.SearchAsync(
            Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenRetainedMatchesExactlyEqualLimit_RemainsComplete()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "a.txt"), "one needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();

        var result = await fileSystem.SearchAsync(
            Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenMatchingLineIsLong_RetainsBoundedContextContainingMatch()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "a.txt"),
            $"{new string('x', 200)}needle{new string('y', 200)}",
            TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem();
        var request = new FileSearchRequest(
            null,
            new FileSearchPattern("needle", FileSearchPatternKind.Literal),
            new GlobPattern("**/*"),
            true,
            false,
            10,
            100,
            1024 * 1024,
            100,
            30,
            TimeSpan.FromSeconds(10),
            TestSecurity.Grant());

        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);

        var match = result.Matches.ShouldHaveSingleItem();
        match.LineText.ShouldContain("needle");
        match.LineTextTruncated.ShouldBeTrue();
        match.LineProjectionByteOffset.ShouldBeGreaterThan(0);
        System.Text.Encoding.UTF8.GetByteCount(match.LineText).ShouldBeLessThanOrEqualTo(30);
    }

    [Fact]
    public async Task SearchAsync_WhenRequestExceedsHostCeiling_DeniesBeforeTraversal()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem(configure: static options => options.MaximumSearchMatches = 1);

        var result = await fileSystem.SearchAsync(
            Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 2),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Denied);
        result.VisitedFiles.ShouldBe(0);
        result.VisitedBytes.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_WhenSymlinkPointsOutsideRoot_DoesNotTraverseOrRevealTarget()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-search-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(outside, "secret.txt"), "needle", TestContext.Current.CancellationToken);
            _ = Directory.CreateSymbolicLink(Path.Combine(_root, "linked"), outside);
            var fileSystem = CreateFileSystem();

            var result = await fileSystem.SearchAsync(
                Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)),
                TestContext.Current.CancellationToken);

            result.Status.ShouldBe(FileSearchStatus.NoMatches);
            result.VisitedFiles.ShouldBe(0);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task SearchAsync_WhenGrantDenied_DoesNotCheckBaseExistence()
    {
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystem(grantStore);
        var request = Request(
            new FileSearchPattern("needle", FileSearchPatternKind.Literal),
            basePath: new FileSystemPath("missing"));

        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileSearch);
        enforcement.Resources.ShouldBe([FileSearchSecurityBinding.Resource(request.BasePath)]);
        enforcement.InputFingerprint.ShouldBe(FileSearchSecurityBinding.Fingerprint(
            request.BasePath,
            request.Pattern,
            request.PathPattern,
            request.CaseSensitive,
            request.IncludeHidden,
            request.MaximumDepth,
            request.MaximumFiles,
            request.MaximumBytes,
            request.MaximumMatches,
            request.MaximumLineBytes,
            request.MaximumDuration));
    }

    [Fact]
    public async Task SearchAsync_WhenDurationElapses_ReturnsTypedTimeout()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_root, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystem(timeProvider: new AdvancingTimeProvider());

        var result = await fileSystem.SearchAsync(
            Request(
                new FileSearchPattern("needle", FileSearchPatternKind.Literal),
                maximumDuration: TimeSpan.FromSeconds(1)),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.TimedOut);
        result.Complete.ShouldBeFalse();
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private SandboxedFileSystem CreateFileSystem(
        ISecurityGrantStore? grantStore = null,
        TimeProvider? timeProvider = null,
        Action<SandboxedFileSystemOptions>? configure = null)
    {
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        configure?.Invoke(options);
        return new SandboxedFileSystem(
            Options.Create(options),
            grantStore ?? TestSecurity.GrantStore(),
            timeProvider ?? TimeProvider.System);
    }

    private static FileSearchRequest Request(
        FileSearchPattern pattern,
        FileSystemPath? basePath = null,
        GlobPattern? pathPattern = null,
        bool caseSensitive = true,
        int maximumMatches = 100,
        TimeSpan? maximumDuration = null) => new(
            basePath,
            pattern,
            pathPattern ?? new GlobPattern("**/*"),
            caseSensitive,
            includeHidden: false,
            maximumDepth: 10,
            maximumFiles: 100,
            maximumBytes: 1024 * 1024,
            maximumMatches,
            maximumLineBytes: 1024,
            maximumDuration ?? TimeSpan.FromSeconds(10),
            TestSecurity.Grant());

    private sealed class AdvancingTimeProvider: TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Increment(ref _timestamp);
    }
}
