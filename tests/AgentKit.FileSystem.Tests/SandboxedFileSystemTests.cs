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
        var exception = Should.Throw<ArgumentNullException>(() => new SandboxedFileSystem(
            null!, TestSecurity.GrantStore(), TimeProvider.System));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenRootDirectoryNotRooted_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "relative/path" });

        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(
            options, TestSecurity.GrantStore(), TimeProvider.System));
    }

    [Fact]
    public void Constructor_WhenRootDirectoryBlank_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "   " });

        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(
            options, TestSecurity.GrantStore(), TimeProvider.System));
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
        var request = new FileWriteRequest(
            new FileSystemPath("notes.txt"), "hello world", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant());

        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);

        var written = result.ShouldBeOfType<FileWritten>();
        written.BytesWritten.ShouldBe(11L);
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("hello world");
    }

    [Fact]
    public async Task WriteAsync_WhenParentDirectoryMissing_DoesNotCreateParentDirectory()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(
            new FileSystemPath("sub/dir/notes.txt"), "nested", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant());

        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteFailed>();
        Directory.Exists(Path.Combine(_root, "sub")).ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenFileExists_ReturnsContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "existing content");

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);

        var read = result.ShouldBeOfType<FileRead>();
        read.Content.ShouldBe("existing content");
        read.Bytes.ShouldBe(16L);
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExist_ReturnsFileNotFound()
    {
        var fs = CreateFileSystem();

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("missing.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_WhenGrantDenied_LeaksNoTargetExistenceAndUsesExactBinding(bool createTarget)
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 1, "Grant does not match."),
        };
        var fs = CreateFileSystem(grantStore: store);
        if (createTarget)
        {
            File.WriteAllText(Path.Combine(_root, "possibly-secret.txt"), "secret");
        }

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("possibly-secret.txt"), TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldBe("Grant does not match.");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Resources.ShouldBe([FileSecurityBinding.Resource(new FileSystemPath("possibly-secret.txt"))]);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.ReadFingerprint(new FileSystemPath("possibly-secret.txt")));
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
            new FileReadRequest(new FileSystemPath("outside-link/secret.txt"), TestSecurity.Grant()),
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
            new FileReadRequest(new FileSystemPath("secret-link.txt"), TestSecurity.Grant()),
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
                FileWriteMode.CreateOrOverwrite,
                TestSecurity.Grant()),
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
                FileWriteMode.CreateOrOverwrite,
                TestSecurity.Grant()),
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
            new FileWriteRequest(
                new FileSystemPath("notes.txt"), "new content", FileWriteMode.CreateNew, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileAlreadyExists>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("already here");
    }

    [Fact]
    public async Task WriteAsync_WhenConcurrentCreateNewTargetsSamePath_CreatesExactlyOnce()
    {
        var fs = CreateFileSystem();
        var first = new FileWriteRequest(
            new FileSystemPath("notes.txt"), "first", FileWriteMode.CreateNew, TestSecurity.Grant());
        var second = new FileWriteRequest(
            new FileSystemPath("notes.txt"), "second", FileWriteMode.CreateNew, TestSecurity.Grant());

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
            FileWriteMode.CreateOrOverwrite,
            TestSecurity.Grant()) with
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
            new FileWriteRequest(
                new FileSystemPath("notes.txt"), "second", FileWriteMode.Append, TestSecurity.Grant()),
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
            new FileWriteRequest(
                new FileSystemPath("notes.txt"), "new", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()),
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

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileReadDenied>();
    }

    [Fact]
    public async Task WriteAsync_WhenContentExceedsMaximumWriteBytes_ReturnsFileWriteDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 4);

        var result = await fs.WriteAsync(
            new FileWriteRequest(
                new FileSystemPath("notes.txt"), "this is too long", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(_root, "notes.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenGrantDenied_DoesNotMutateAndUsesExactDispositionBinding()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Revoked, 1, "Grant is revoked."),
        };
        var fs = CreateFileSystem(grantStore: store);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "original");

        var result = await fs.WriteAsync(
            new FileWriteRequest(
                new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<FileWriteDenied>().SafeMessage.ShouldBe("Grant is revoked.");
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("original");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.WriteFingerprint(
            new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite));
    }

    [Fact]
    public async Task ReadAsync_WhenRootDirectoryDoesNotExist_ReturnsFileNotFound()
    {
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        var fs = new SandboxedFileSystem(Options.Create(options), TestSecurity.GrantStore(), TimeProvider.System);

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExistInExistingRoot_ReturnsFileNotFound()
    {
        var fs = CreateFileSystem();

        var result = await fs.ReadAsync(
            new FileReadRequest(new FileSystemPath("file.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task EnumerateAsync_WhenRootContainsEntries_ReturnsOrdinalPagesWithStableCursor()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        _ = Directory.CreateDirectory(Path.Combine(_root, "c"));

        var first = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(null, 2, null, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);
        var second = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(null, 2, first.Continuation, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        first.Status.ShouldBe(DirectoryEnumerationStatus.Success);
        first.Entries.Select(static entry => entry.Path.Value).ShouldBe(["a.txt", "b.txt"]);
        _ = first.Continuation.ShouldNotBeNull();
        second.Status.ShouldBe(DirectoryEnumerationStatus.Success);
        second.Entries.Select(static entry => entry.Path.Value).ShouldBe(["c"]);
        second.Continuation.ShouldBeNull();
        second.SnapshotFingerprint.ShouldBe(first.SnapshotFingerprint);
    }

    [Fact]
    public async Task EnumerateAsync_WhenDirectoryChangesBetweenPages_ReturnsSnapshotChanged()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        var first = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(_root, "c.txt"), "c");

        var resumed = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(null, 1, first.Continuation, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        resumed.Status.ShouldBe(DirectoryEnumerationStatus.SnapshotChanged);
        resumed.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnumerateAsync_WhenSnapshotExceedsConfiguredBound_ReturnsLimitExceeded()
    {
        var fs = CreateFileSystem(options => options.MaximumDirectorySnapshotEntries = 2);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "c.txt"), "c");

        var result = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DirectoryEnumerationStatus.LimitExceeded);
    }

    [Fact]
    public async Task EnumerateAsync_WhenPathTraversesSymbolicLink_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside"), _outsideRoot);

        var result = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(new FileSystemPath("outside"), 10, null, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DirectoryEnumerationStatus.Denied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnumerateAsync_WhenGrantDenied_LeaksNoDirectoryExistenceAndUsesExactBinding(bool createDirectory)
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 1, "Grant does not match."),
        };
        var fs = CreateFileSystem(grantStore: store);
        if (createDirectory)
        {
            _ = Directory.CreateDirectory(Path.Combine(_root, "possibly-secret"));
        }

        var path = new FileSystemPath("possibly-secret");
        var result = await fs.EnumerateAsync(
            new DirectoryEnumerationRequest(path, 7, null, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DirectoryEnumerationStatus.Denied);
        result.SafeMessage.ShouldBe("Grant does not match.");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.DirectoryRead);
        enforcement.Resources.ShouldBe([DirectorySecurityBinding.Resource(path)]);
        enforcement.InputFingerprint.ShouldBe(DirectorySecurityBinding.Fingerprint(path, 7, null));
    }

    [Fact]
    public async Task GlobAsync_WhenRecursivePatternMatches_ReturnsDeterministicCompleteResults()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(Path.Combine(_root, "src", "nested"));
        File.WriteAllText(Path.Combine(_root, "z.cs"), "z");
        File.WriteAllText(Path.Combine(_root, "src", "b.cs"), "b");
        File.WriteAllText(Path.Combine(_root, "src", "nested", "a.cs"), "a");
        File.WriteAllText(Path.Combine(_root, "src", "nested", "note.txt"), "text");

        var result = await fs.GlobAsync(
            new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.Success);
        result.Complete.ShouldBeTrue();
        result.Matches.Select(static path => path.Value).ShouldBe(["src/b.cs", "src/nested/a.cs", "z.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenHiddenExcluded_DoesNotVisitDotPrefixedSubtrees()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "secret-ish");
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "visible");

        var result = await fs.GlobAsync(
            new GlobRequest(null, new GlobPattern("**/*"), true, false, 10, 100, 20, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.Success);
        result.Matches.Select(static path => path.Value).ShouldBe(["visible.txt"]);
        result.VisitedEntries.ShouldBe(1);
    }

    [Fact]
    public async Task GlobAsync_WhenNoNameMatches_ReturnsDistinctNoMatchesOutcome()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");

        var result = await fs.GlobAsync(
            new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.NoMatches);
        result.Complete.ShouldBeTrue();
        result.Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task GlobAsync_WhenVisitBoundExceeded_ReturnsPartialIncompleteLimitOutcome()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a.cs"), "a");
        File.WriteAllText(Path.Combine(_root, "b.cs"), "b");

        var result = await fs.GlobAsync(
            new GlobRequest(null, new GlobPattern("*.cs"), true, false, 1, 1, 10, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.VisitedEntries.ShouldBe(2);
        result.Matches.Select(static path => path.Value).ShouldBe(["a.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenSymlinkPointsOutside_DoesNotTraverseTarget()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        File.WriteAllText(Path.Combine(_outsideRoot, "secret.cs"), "secret");
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside"), _outsideRoot);

        var result = await fs.GlobAsync(
            new GlobRequest(null, new GlobPattern("**/*.cs"), true, true, 10, 100, 20, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task GlobAsync_WhenGrantDenied_DoesNotObserveAndUsesExactBinding()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Revoked, 1, "Grant is revoked."),
        };
        var fs = CreateFileSystem(grantStore: store);
        var pattern = new GlobPattern("**/*.cs");

        var result = await fs.GlobAsync(
            new GlobRequest(null, pattern, true, false, 7, 80, 9, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.Denied);
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Resources.ShouldBe([GlobSecurityBinding.Resource(null)]);
        enforcement.InputFingerprint.ShouldBe(GlobSecurityBinding.Fingerprint(null, pattern, true, false, 7, 80, 9));
    }

    [Fact]
    public async Task ReadAsync_WhenObserved_EmitsSecurityCorrelatedActivityWithoutRawPath()
    {
        const string protectedPath = "content-must-not-enter-diagnostics.txt";
        var request = new FileReadRequest(new FileSystemPath(protectedPath), TestSecurity.Grant());
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.FileSystemOperation
                    && Equals(activity.GetTagItem(AgentKitTagNames.SecurityRequestId), request.Grant.RequestId.ToString()))
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var fs = CreateFileSystem();

        _ = await fs.ReadAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.FileSystemOperation).ShouldBe("read");
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedPath);
    }

    private SandboxedFileSystem CreateFileSystem(
        Action<SandboxedFileSystemOptions>? configure = null,
        ISecurityGrantStore? grantStore = null)
    {
        _ = Directory.CreateDirectory(_root);
        var options = new SandboxedFileSystemOptions { RootDirectory = _root };
        configure?.Invoke(options);
        return new SandboxedFileSystem(
            Options.Create(options), grantStore ?? TestSecurity.GrantStore(), TimeProvider.System);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
