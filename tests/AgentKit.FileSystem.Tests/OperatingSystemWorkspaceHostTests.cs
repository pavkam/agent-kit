// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;



/// <summary>Verifies OperatingSystemWorkspaceHost behavior and contracts.</summary>
public sealed class OperatingSystemWorkspaceHostTests: IDisposable
{
    public OperatingSystemWorkspaceHostTests()
    {
        _ = Directory.CreateDirectory(_rootOperatingSystemWorkspaceHostSearch);
        _ = Directory.CreateDirectory(_rootOperatingSystemWorkspaceHostEdit);
        _ = Directory.CreateDirectory(_rootOperatingSystemWorkspaceHostPatch);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentkit-fs-tests-" + Guid.NewGuid().ToString("N"));

    private readonly string _outsideRoot = Path.Combine(Path.GetTempPath(), "agentkit-fs-outside-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var profile = WorkspaceHostTestFactory.Snapshot(new WorkspaceHostOptions { RootDirectory = _root });
        var store = TestSecurity.GrantStore();
        var intents = new GuidSecurityEnforcementIntentIdGenerator();

        Should.Throw<ArgumentNullException>(() => new OperatingSystemWorkspaceHost(null!, store, TimeProvider.System, null, intents))
            .ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new OperatingSystemWorkspaceHost(profile, null!, TimeProvider.System, null, intents))
            .ParamName.ShouldBe("grantStore");
        Should.Throw<ArgumentNullException>(() => new OperatingSystemWorkspaceHost(profile, store, null!, null, intents))
            .ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new OperatingSystemWorkspaceHost(profile, store, TimeProvider.System, null, null!))
            .ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenProfileDeclaresNoRoot_ThrowsArgumentException()
    {
        var profile = WorkspaceHostTestFactory.Snapshot(new WorkspaceHostOptions { RootDirectory = _root }) with { Roots = [] };

        var exception = Should.Throw<ArgumentException>(
            () => new OperatingSystemWorkspaceHost(profile, TestSecurity.GrantStore(), TimeProvider.System, null, new GuidSecurityEnforcementIntentIdGenerator()));

        exception.ParamName.ShouldBe("profile");
    }

    [Fact]
    public void SecurityAudience_WhenProfileIsBound_IsScopedToTheProfileKey() =>
        CreateFileSystem().SecurityAudience.ShouldBe(new ComponentId("agentkit.filesystem.os.test"));

    [Fact]
    public async Task EnumerateAsync_WhenRootContainsEntries_YieldsOrdinalOrderWithDirectoryFlags()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        _ = Directory.CreateDirectory(Path.Combine(_root, "c"));

        var entries = await EnumerateAsync(fs, null);

        entries.Select(static entry => (entry.Name.Value, entry.IsDirectory))
            .ShouldBe([("a.txt", false), ("b.txt", false), ("c", true)]);
    }

    [Fact]
    public async Task EnumerateAsync_WhenPathNamesASubdirectory_YieldsOnlyItsChildren()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(Path.Combine(_root, "src", "nested"));
        File.WriteAllText(Path.Combine(_root, "src", "main.cs"), "m");
        File.WriteAllText(Path.Combine(_root, "root.txt"), "r");

        var entries = await EnumerateAsync(fs, new FileSystemPath("src"));

        entries.Select(static entry => (entry.Name.Value, entry.IsDirectory)).ShouldBe([("main.cs", false), ("nested", true)]);
    }

    [Fact]
    public async Task EnumerateAsync_WhenEntryIsASymbolicLinkToADirectory_ReportsItAsNotADirectory()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "link"), _outsideRoot);

        var entries = await EnumerateAsync(fs, null);

        entries.Select(static entry => (entry.Name.Value, entry.IsDirectory)).ShouldBe([("link", false)]);
    }

    [Fact]
    public async Task EnumerateAsync_WhenSnapshotExceedsConfiguredBound_ThrowsIOExceptionWithoutPaths()
    {
        var fs = CreateFileSystem(options => options.MaximumDirectorySnapshotEntries = 2);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "c.txt"), "c");

        var exception = await Should.ThrowAsync<IOException>(async () => await EnumerateAsync(fs, null));

        exception.ShouldNotBeOfType<DirectoryNotFoundException>();
        exception.Message.ShouldBe("The directory exceeds the configured snapshot limit of 2 entries.");
    }

    [Fact]
    public async Task EnumerateAsync_WhenPathTraversesSymbolicLink_ThrowsUnauthorizedAccessException()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        File.WriteAllText(Path.Combine(_outsideRoot, "secret.txt"), "secret");
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside"), _outsideRoot);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(async () => await EnumerateAsync(fs, new FileSystemPath("outside")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnumerateAsync_WhenGrantDenied_LeaksNoDirectoryExistenceAndUsesExactBinding(bool createDirectory)
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, 1, "Grant does not match.", null),
        };
        var fs = CreateFileSystem(grantStore: store);
        if (createDirectory)
        {
            _ = Directory.CreateDirectory(Path.Combine(_root, "possibly-secret"));
        }

        var path = new FileSystemPath("possibly-secret");

        var exception = await Should.ThrowAsync<UnauthorizedAccessException>(async () => await EnumerateAsync(fs, path));

        exception.Message.ShouldBe("Grant does not match.");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.DirectoryRead);
        enforcement.Audience.ShouldBe(fs.SecurityAudience);
        enforcement.Resources.ShouldBe([DirectorySecurityBinding.Resource(path)]);
        enforcement.InputFingerprint.ShouldBe(DirectorySecurityBinding.Fingerprint(path));
    }

    [Fact]
    public async Task EnumerateAsync_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        var fs = CreateFileSystem();

        _ = await Should.ThrowAsync<DirectoryNotFoundException>(async () => await EnumerateAsync(fs, new FileSystemPath("missing")));
    }

    [Fact]
    public async Task EnumerateAsync_WhenNestedPathParentIsMissing_ThrowsDirectoryNotFoundException()
    {
        var fs = CreateFileSystem();

        _ = await Should.ThrowAsync<DirectoryNotFoundException>(async () => await EnumerateAsync(fs, new FileSystemPath("missing/sub")));
    }

    [Fact]
    public async Task EnumerateAsync_WhenEntryNameContainsBackslash_ThrowsIOException()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a\\b.txt"), "content");

        var exception = await Should.ThrowAsync<IOException>(async () => await EnumerateAsync(fs, null));

        exception.Message.ShouldBe("The directory contains a name that cannot be represented by this path profile.");
    }

    [Fact]
    public async Task EnumerateAsync_WhenEntryNameIsWhitespaceOnly_ThrowsIOExceptionInsteadOfArgumentException()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, " "), "content");

        _ = await Should.ThrowAsync<IOException>(async () => await EnumerateAsync(fs, null));
    }

    [Fact]
    public void EnumerateAsync_WhenOperationIsNull_ThrowsArgumentNullExceptionBeforeEnumerating()
    {
        var fs = CreateFileSystem();

        var exception = Should.Throw<ArgumentNullException>(() => fs.EnumerateAsync(null!));

        exception.ParamName.ShouldBe("operation");
    }

    [Fact]
    public async Task EnumerateAsync_WhenCallerCancelsDuringGrantConsumption_PropagatesCancellationWithoutObservingDirectory()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestSecurity.RecordingGrantStore { OnIntentConsumption = cancellation.Cancel };
        var fs = CreateFileSystem(grantStore: store);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await EnumerateWithTokenAsync(fs, null, grant: null, cancellation.Token));
    }

    [Fact]
    public async Task EnumerateAsync_WhenGrantStoreThrowsUnexpectedException_RethrowsIt()
    {
        var fs = CreateFileSystem(grantStore: new ThrowingGrantStore());

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await EnumerateAsync(fs, null));
    }

    [Fact]
    public async Task EnumerateAsync_WhenObserved_EmitsSecurityCorrelatedActivityWithoutRawPath()
    {
        const string protectedPath = "content-must-not-enter-diagnostics";
        _ = Directory.CreateDirectory(Path.Combine(_root, protectedPath));
        var grant = TestSecurity.Grant();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.FileSystemOperation
                    && Equals(activity.GetTagItem(AgentKitTagNames.SecurityRequestId), grant.RequestId.ToString()))
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var fs = CreateFileSystem();

        _ = await EnumerateAsync(fs, new FileSystemPath(protectedPath), grant);

        var observed = stopped.ShouldNotBeNull();
        observed.GetTagItem(AgentKitTagNames.FileSystemOperation).ShouldBe("enumerate");
        observed.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedPath);
    }

    [Fact]
    public async Task EnumerateAsync_WhenLoggerIsEnabledAndEnumerationSucceeds_EmitsCompletedStructuredEvent()
    {
        var logger = new RecordingLogger<OperatingSystemWorkspaceHost>();
        var fs = CreateFileSystem(logger: logger);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "content");
        var grant = TestSecurity.Grant();

        _ = await EnumerateAsync(fs, null, grant);

        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(11000);
        completed.State["Operation"].ShouldBe("enumerate");
        completed.State["SecurityRequestId"].ShouldBe(grant.RequestId);
        completed.State["Outcome"].ShouldBe("Success");
        completed.Message.ShouldNotContain("notes.txt");
    }

    [Fact]
    public async Task EnumerateAsync_WhenLoggerIsEnabledAndGrantStoreThrowsUnexpectedException_EmitsFailedStructuredEvent()
    {
        var logger = new RecordingLogger<OperatingSystemWorkspaceHost>();
        var fs = CreateFileSystem(grantStore: new ThrowingGrantStore(), logger: logger);
        var grant = TestSecurity.Grant();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await EnumerateAsync(fs, null, grant));

        var failed = logger.Snapshot().Single(static entry => entry.EventId.Id == 11001);
        failed.State["Operation"].ShouldBe("enumerate");
        failed.State["SecurityRequestId"].ShouldBe(grant.RequestId);
        failed.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    private Task<List<FileSystemEntry>> EnumerateAsync(
        OperatingSystemWorkspaceHost host,
        FileSystemPath? path,
        SecurityGrant? grant = null) =>
        EnumerateWithTokenAsync(host, path, grant, TestContext.Current.CancellationToken);

    private async Task<List<FileSystemEntry>> EnumerateWithTokenAsync(
        OperatingSystemWorkspaceHost host,
        FileSystemPath? path,
        SecurityGrant? grant,
        CancellationToken cancellationToken)
    {
        var relative = path?.Value ?? ".";
        var target = new ResolvedFileTarget(
            new FileRootId("workspace"),
            new NormalizedRelativePath(relative),
            Path.GetFullPath(Path.Combine(_root, relative)),
            FilePathComparisonKind.Ordinal,
            FileSecurityBinding.ContentFingerprint("no-link"u8),
            FileSecurityBinding.ContentFingerprint("target"u8));
        var entries = new List<FileSystemEntry>();
        await foreach (var entry in host.EnumerateAsync(
            new AuthorizedDirectoryEnumeration(target, grant ?? TestSecurity.Grant()),
            cancellationToken))
        {
            entries.Add(entry);
        }

        return entries;
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
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Success);
        result.Matches.Select(static path => path.Value).ShouldBe(["visible.txt"]);
        result.VisitedEntries.ShouldBe(1);
    }

    [Fact]
    public async Task GlobAsync_WhenSubtreeExcluded_PrunesItBeforeVisitBoundIsConsumed()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(Path.Combine(_root, "bin", "generated"));
        _ = Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "bin", "generated", "one.cs"), "generated");
        File.WriteAllText(Path.Combine(_root, "bin", "generated", "two.cs"), "generated");
        File.WriteAllText(Path.Combine(_root, "src", "target.cs"), "source");
        var request = new GlobRequest(
            null, new GlobPattern("**/*.cs"), true, false, 10, 3, 20, TestSecurity.Grant())
        {
            ExcludedPathPatterns = [new GlobPattern("**/bin/**")],
        };

        var result = await fs.GlobAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GlobStatus.Success);
        result.Complete.ShouldBeTrue();
        result.VisitedEntries.ShouldBe(3);
        result.Matches.Select(static path => path.Value).ShouldBe(["src/target.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenNoNameMatches_ReturnsDistinctNoMatchesOutcome()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("*.cs"), true, false, 1, 1, 10, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, true, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task GlobAsync_WhenEntryNameIsWhitespaceOnly_ReturnsFailedInsteadOfThrowing()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, " "), "content");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Failed);
    }

    [Fact]
    public async Task GlobAsync_WhenBaseDirectoryDoesNotExist_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.GlobAsync(new GlobRequest(new FileSystemPath("missing"), new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.NotFound);
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task GlobAsync_WhenBaseDirectoryIsSymlinkOutsideRoot_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_outsideRoot);
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside-base"), _outsideRoot);
        var result = await fs.GlobAsync(new GlobRequest(new FileSystemPath("outside-base"), new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Denied);
    }

    [Fact]
    public async Task GlobAsync_WhenGrantDenied_DoesNotObserveAndUsesExactBinding()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Revoked, 1, "Grant is revoked.", null),
        };
        var fs = CreateFileSystem(grantStore: store);
        var pattern = new GlobPattern("**/*.cs");
        var result = await fs.GlobAsync(new GlobRequest(null, pattern, true, false, 7, 80, 9, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Denied);
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Resources.ShouldBe([GlobSecurityBinding.Resource(null)]);
        enforcement.InputFingerprint.ShouldBe(GlobSecurityBinding.Fingerprint(null, pattern, true, false, 7, 80, 9));
    }

    [Fact]
    public async Task GlobAsync_WhenRetainedResultLimitReached_ReturnsLimitExceededDuringMatchRetention()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "a.cs"), "a");
        File.WriteAllText(Path.Combine(_root, "b.cs"), "b");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("*.cs"), true, false, 10, 100, 1, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.Matches.Select(static path => path.Value).ShouldBe(["a.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenLimitIsReachedDeepInTraversal_PropagatesTerminalStatusToAncestor()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "other.cs"), "other");
        File.WriteAllText(Path.Combine(_root, "sub", "deep.cs"), "deep");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 5, 2, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.VisitedEntries.ShouldBe(3);
        result.Matches.Select(static path => path.Value).ShouldBe(["other.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenSubdirectoryPermissionDenied_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fs = CreateFileSystem();
        var restricted = Path.Combine(_root, "restricted");
        _ = Directory.CreateDirectory(restricted);
        File.WriteAllText(Path.Combine(restricted, "secret.cs"), "secret");
        File.SetUnixFileMode(restricted, UnixFileMode.None);
        try
        {
            var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(GlobStatus.Denied);
        }
        finally
        {
            File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed class ThrowingGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected grant store failure.");

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }

    private OperatingSystemWorkspaceHost CreateFileSystem(Action<WorkspaceHostOptions>? configure = null, ISecurityGrantStore? grantStore = null, ILogger<OperatingSystemWorkspaceHost>? logger = null)
    {
        _ = Directory.CreateDirectory(_root);
        var options = new WorkspaceHostOptions
        {
            RootDirectory = _root
        };
        configure?.Invoke(options);
        return WorkspaceHostTestFactory.Create(options, grantStore ?? TestSecurity.GrantStore(), TimeProvider.System, logger);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;

    private readonly string _rootOperatingSystemWorkspaceHostSearch = Path.Combine(Path.GetTempPath(), $"agentkit-search-{Guid.NewGuid():N}");

    [Fact]
    public async Task SearchAsync_WhenLiteralMatches_ReturnsDeterministicVersionedByteLocations()
    {
        _ = Directory.CreateDirectory(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src", "b.cs"), "first\nNeedle café\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src", "a.cs"), "needle\n", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), caseSensitive: false), TestContext.Current.CancellationToken);
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
        _ = Directory.CreateDirectory(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src", "code.cs"), "item-42 item-x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src", "notes.md"), "item-99", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern( /*lang=regex*/"item-[0-9]+", FileSearchPatternKind.RegularExpression), pathPattern: new GlobPattern("**/*.cs")), TestContext.Current.CancellationToken);
        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/code.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenSubtreeExcluded_PrunesItBeforeCandidateFileBoundIsConsumed()
    {
        _ = Directory.CreateDirectory(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "bin", "generated"));
        _ = Directory.CreateDirectory(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "bin", "generated", "one.cs"),
            "needle",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "bin", "generated", "two.cs"),
            "needle",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "src", "target.cs"),
            "needle",
            TestContext.Current.CancellationToken);
        var request = new FileSearchRequest(
            null,
            new FileSearchPattern("needle", FileSearchPatternKind.Literal),
            new GlobPattern("**/*.cs"),
            true,
            false,
            10,
            1,
            1024,
            10,
            1024,
            TimeSpan.FromSeconds(10),
            TestSecurity.Grant())
        {
            ExcludedPathPatterns = [new GlobPattern("**/bin/**")],
        };

        var result = await CreateFileSystemOperatingSystemWorkspaceHostSearch().SearchAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        result.VisitedFiles.ShouldBe(1);
        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/target.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenFilesAreHiddenBinaryOrInvalidUtf8_ExcludesThemWithoutDecoding()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, ".hidden"), "needle", TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "binary"), "needle\0tail"u8.ToArray(), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "invalid"), [0x6e, 0x65, 0x65, 0x64, 0x6c, 0x65, 0xff], TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
        result.VisitedFiles.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchLimitReached_ReturnsTypedPartialResult()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), "needle needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenRetainedMatchesExactlyEqualLimit_RemainsComplete()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), "one needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenMatchingLineIsLong_RetainsBoundedContextContainingMatch()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), $"{new string('x', 200)}needle{new string('y', 200)}", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 1024 * 1024, 100, 30, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        var match = result.Matches.ShouldHaveSingleItem();
        match.LineText.ShouldContain("needle");
        match.LineTextTruncated.ShouldBeTrue();
        match.LineProjectionByteOffset.ShouldBeGreaterThan(0);
        System.Text.Encoding.UTF8.GetByteCount(match.LineText).ShouldBeLessThanOrEqualTo(30);
    }

    [Fact]
    public async Task SearchAsync_WhenProjectionWindowWouldSplitAMultiByteCharacter_AdjustsBothEdgesToValidUtf8()
    {
        // "é" is 2 UTF-8 bytes (0xC3 0xA9); with MaximumLineBytes=29 the naive half-context math lands the
        // leading edge on a continuation byte and the trailing edge mid-character, forcing both the
        // leading-edge continuation-byte skip and the trailing shrink-until-valid retry.
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), $"{new string('é', 100)}needle{new string('é', 100)}", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 1024 * 1024, 100, 29, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        var match = result.Matches.ShouldHaveSingleItem();
        match.LineText.ShouldContain("needle");
        match.LineTextTruncated.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetByteCount(match.LineText).ShouldBeLessThanOrEqualTo(29);
    }

    [Fact]
    public async Task SearchAsync_WhenRequestExceedsHostCeiling_DeniesBeforeTraversal()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch(configure: static options => options.MaximumSearchMatches = 1);
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 2), TestContext.Current.CancellationToken);
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
            await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "needle", TestContext.Current.CancellationToken);
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "linked"), outside);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
            var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
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
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied.", null),
        };
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch(grantStore);
        var request = Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("missing"));
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileSearch);
        enforcement.Resources.ShouldBe([FileSearchSecurityBinding.Resource(request.BasePath)]);
        enforcement.InputFingerprint.ShouldBe(FileSearchSecurityBinding.Fingerprint(request.BasePath, request.Pattern, request.PathPattern, request.CaseSensitive, request.IncludeHidden, request.MaximumDepth, request.MaximumFiles, request.MaximumBytes, request.MaximumMatches, request.MaximumLineBytes, request.MaximumDuration));
    }

    [Fact]
    public async Task SearchAsync_WhenDurationElapses_ReturnsTypedTimeout()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch(timeProvider: new AdvancingTimeProvider());
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumDuration: TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.TimedOut);
        result.Complete.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_WhenEntryNameIsWhitespaceOnly_ReturnsFailedInsteadOfThrowing()
    {
        File.WriteAllText(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, " "), "needle");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Failed);
    }

    [Fact]
    public async Task SearchAsync_WhenBaseDirectoryDoesNotExist_ReturnsNotFound()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var request = Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("missing"));
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NotFound);
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_WhenBaseDirectoryIsSymlinkOutsideRoot_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-search-base-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "outside-base"), outside);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
            var request = Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("outside-base"));
            var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
            result.Status.ShouldBe(FileSearchStatus.Denied);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task SearchAsync_WhenSubdirectoryPermissionDenied_ReturnsDeniedWithoutVisitingItsContent()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var restricted = Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "restricted");
        _ = Directory.CreateDirectory(restricted);
        await File.WriteAllTextAsync(Path.Combine(restricted, "secret.txt"), "needle", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(restricted, UnixFileMode.None);
        try
        {
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
            var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(FileSearchStatus.Denied);
            result.VisitedFiles.ShouldBe(0);
        }
        finally
        {
            File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task SearchAsync_WhenCandidateFilePermissionDenied_ReturnsDeniedWithoutReadingContent()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var restricted = Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "secret.txt");
        await File.WriteAllTextAsync(restricted, "needle", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(restricted, UnixFileMode.None);
        try
        {
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
            var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(FileSearchStatus.Denied);
        }
        finally
        {
            File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task SearchAsync_WhenLaterSiblingFollowsATerminatedSubtree_SkipsItWithoutOpening()
    {
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            var directory = Path.Combine(_rootOperatingSystemWorkspaceHostSearch, name);
            _ = Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "needle.txt"), "needle", TestContext.Current.CancellationToken);
        }

        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 2, 1024 * 1024, 100, 1024, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.VisitedFiles.ShouldBe(2);
        result.Matches.Select(static match => match.Path.Value).ShouldBe(["a/needle.txt", "b/needle.txt"]);
    }

    [Fact]
    public async Task SearchAsync_WhenCandidateExceedsRemainingByteBudget_ReturnsLimitExceeded()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "big.txt"), new string('x', 100), TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 10, 100, 1024, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fileSystem.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("observed-byte");
    }

    [Fact]
    public async Task SearchAsync_WhenCandidateIsANamedPipe_SkipsItWithoutHangingOrMatching()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fifoPath = Path.Combine(_rootOperatingSystemWorkspaceHostSearch, "fifo.txt");
        var mkfifo = Process.Start("mkfifo", fifoPath);
        await mkfifo.WaitForExitAsync(TestContext.Current.CancellationToken);
        mkfifo.ExitCode.ShouldBe(0);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
    }

    private OperatingSystemWorkspaceHost CreateFileSystemOperatingSystemWorkspaceHostSearch(ISecurityGrantStore? grantStore = null, TimeProvider? timeProvider = null, Action<WorkspaceHostOptions>? configure = null)
    {
        var options = new WorkspaceHostOptions
        {
            RootDirectory = _rootOperatingSystemWorkspaceHostSearch
        };
        configure?.Invoke(options);
        return WorkspaceHostTestFactory.Create(options, grantStore ?? TestSecurity.GrantStore(), timeProvider ?? TimeProvider.System);
    }

    private static FileSearchRequest Request(FileSearchPattern pattern, FileSystemPath? basePath = null, GlobPattern? pathPattern = null, bool caseSensitive = true, int maximumMatches = 100, TimeSpan? maximumDuration = null) => new(basePath, pattern, pathPattern ?? new GlobPattern("**/*"), caseSensitive, includeHidden: false, maximumDepth: 10, maximumFiles: 100, maximumBytes: 1024 * 1024, maximumMatches, maximumLineBytes: 1024, maximumDuration ?? TimeSpan.FromSeconds(10), TestSecurity.Grant());

    private sealed class AdvancingTimeProvider: TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Increment(ref _timestamp);
    }

    private readonly string _rootOperatingSystemWorkspaceHostEdit = Path.Combine(Path.GetTempPath(), $"agentkit-edit-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReadSnapshotAsync_WhenSuccessful_ReturnsExactBytesHashAndEnforcement()
    {
        var bytes = new byte[]
        {
            0xef,
            0xbb,
            0xbf,
            0x61,
            0x0d,
            0x0a
        };
        await File.WriteAllBytesAsync(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt"), bytes, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit(grantStore);
        var request = new FileSnapshotRequest(new FileSystemPath("a.txt"), 100, TestSecurity.Grant());
        var result = await fileSystem.ReadSnapshotAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Success);
        result.Content.ShouldBe(bytes);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(bytes));
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileRead);
        enforcement.Effect.ShouldBe(SecurityEffect.Observe);
        enforcement.Resources.ShouldBe([FileSecurityBinding.Resource(request.Path)]);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.SnapshotFingerprint(request.Path, 100));
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenGrantDenied_DoesNotRevealMissingTarget()
    {
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied.", null),
        };
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit(grantStore);
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenRequestMaximumBytesExceedsConfiguredBound_ReturnsDenied()
    {
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostEdit, MaximumReadBytes = 10 },
            TestSecurity.GrantStore(),
            TimeProvider.System);
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("a.txt"), 1000, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("host boundary");
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenParentDirectoryIsMissing_ReturnsNotFound()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing/sub.txt"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.NotFound);
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenTargetFileIsMissingButParentExists_ReturnsNotFound()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing.txt"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.NotFound);
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenParentIsSymlinkOutsideRoot_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-snapshot-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "outside-link"), outside);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
            var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("outside-link/secret.txt"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(FileSnapshotStatus.Denied);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenTargetIsSymlinkOutsideRoot_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-snapshot-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            var outsidePath = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(outsidePath, "outside", TestContext.Current.CancellationToken);
            _ = File.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "secret-link.txt"), outsidePath);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
            var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("secret-link.txt"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(FileSnapshotStatus.Denied);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenFileExceedsRequestByteBound_ReturnsLimitExceeded()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "big.txt"), "0123456789", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("big.txt"), 4, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.LimitExceeded);
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenTargetIsNamedPipe_ReturnsFailedNotSeekable()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var fifoPath = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "fifo");
        var mkfifo = Process.Start("mkfifo", fifoPath);
        await mkfifo.WaitForExitAsync(TestContext.Current.CancellationToken);
        mkfifo.ExitCode.ShouldBe(0);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("fifo"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("seekable");
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionMatches_CommitsAtomicallyAndPreservesMode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "script.sh");
        await File.WriteAllTextAsync(target, "old\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var original = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var replacement = "new\r\n"u8.ToArray().ToImmutableArray();
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit(grantStore);
        var request = ReplaceRequest("script.sh", FileSecurityBinding.ContentFingerprint(original), replacement, MutationId(1));
        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(replacement.AsSpan()));
        (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)).ShouldBe(replacement);
        File.GetUnixFileMode(target).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostEdit, ".agentkit-stage-*").ShouldBeEmpty();
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Replace);
        enforcement.Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path));
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionChanged_ReturnsConflictWithoutStaging()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var request = ReplaceRequest("a.txt", new ContentHash("sha256:stale"), "replacement"u8.ToArray().ToImmutableArray(), MutationId(2));
        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Conflict);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostEdit, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenGrantDenied_CreatesNoStagingFile()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied.", null),
        };
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit(grantStore);
        var result = await fileSystem.ReplaceAsync(ReplaceRequest("a.txt", FileSecurityBinding.ContentFingerprint(bytes), "replacement"u8.ToArray().ToImmutableArray(), MutationId(3)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostEdit, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenContentExceedsMaximumWriteBytes_ReturnsDenied()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostEdit, MaximumWriteBytes = 2 },
            TestSecurity.GrantStore(),
            TimeProvider.System);
        var expected = FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        var result = await fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "too long"u8.ToArray().ToImmutableArray(), MutationId(6)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
    }

    [Fact]
    public async Task ReplaceAsync_WhenParentDirectoryMissing_ReturnsNotFound()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReplaceAsync(
            ReplaceRequest("missing-parent/a.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(7)),
            TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.NotFound);
    }

    [Fact]
    public async Task ReplaceAsync_WhenParentIsSymlinkOutsideRoot_ReturnsDenied()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-replace-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "outside-link"), outside);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
            var result = await fileSystem.ReplaceAsync(
                ReplaceRequest("outside-link/a.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(8)),
                TestContext.Current.CancellationToken);
            result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ReplaceAsync_WhenTargetMissingButParentExists_ReturnsNotFound()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var result = await fileSystem.ReplaceAsync(
            ReplaceRequest("missing.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(9)),
            TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.NotFound);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("does not exist");
    }

    [Fact]
    public async Task ReplaceAsync_WhenTargetIsSymlinkOutsideRoot_ReturnsDeniedWithoutOutsideEffect()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-replace-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            var outsidePath = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(outsidePath, "outside", TestContext.Current.CancellationToken);
            _ = File.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "secret-link.txt"), outsidePath);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
            var result = await fileSystem.ReplaceAsync(
                ReplaceRequest("secret-link.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(10)),
                TestContext.Current.CancellationToken);
            result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
            (await File.ReadAllTextAsync(outsidePath, TestContext.Current.CancellationToken)).ShouldBe("outside");
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ReplaceAsync_WhenCurrentTargetExceedsReadBoundary_ReturnsFailedNotConflict()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "0123456789", TestContext.Current.CancellationToken);
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostEdit, MaximumReadBytes = 4 },
            TestSecurity.GrantStore(),
            TimeProvider.System);
        var result = await fileSystem.ReplaceAsync(
            ReplaceRequest("a.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(11)),
            TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("boundary");
    }

    [Fact]
    public async Task ReplaceAsync_WhenCallerCancelsWhileWaitingForAnotherPlanOnTheSamePath_ReleasesQueuePositionWithoutHarm()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);
        var expected = FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        var gate = new GatedGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit(gate);
        var firstTask = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "first"u8.ToArray().ToImmutableArray(), MutationId(12)), TestContext.Current.CancellationToken).AsTask();
        await gate.Entered.Task;
        using var cts = new CancellationTokenSource();
        var secondTask = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "second"u8.ToArray().ToImmutableArray(), MutationId(13)), cts.Token).AsTask();
        await cts.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => secondTask);
        gate.Release();
        var firstResult = await firstTask;
        firstResult.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        var thirdResult = await fileSystem.ReplaceAsync(ReplaceRequest("a.txt", FileSecurityBinding.ContentFingerprint("first"u8.ToArray()), "third"u8.ToArray().ToImmutableArray(), MutationId(14)), TestContext.Current.CancellationToken);
        thirdResult.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
    }

    private sealed class GatedGrantStore: ISecurityGrantStore
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
        {
            _ = Entered.TrySetResult();
            await _release.Task.ConfigureAwait(false);
            return new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                0,
                "Consumed by gated test store.",
                new SecurityEnforcementIntentReceipt(
                    intent.Id,
                    grant.Id,
                    grant.RequestId,
                    enforcement,
                    intent.RequiredFence,
                    SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                    DateTimeOffset.UnixEpoch));
        }

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }

    [Fact]
    public async Task ReplaceAsync_WhenTwoPlansRace_OnlyOneExpectedVersionCommits()
    {
        var target = Path.Combine(_rootOperatingSystemWorkspaceHostEdit, "a.txt");
        await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);
        var expected = FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostEdit();
        var first = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "first"u8.ToArray().ToImmutableArray(), MutationId(4)), TestContext.Current.CancellationToken).AsTask();
        var second = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "second"u8.ToArray().ToImmutableArray(), MutationId(5)), TestContext.Current.CancellationToken).AsTask();
        var results = await Task.WhenAll(first, second);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Committed).ShouldBe(1);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Conflict).ShouldBe(1);
        var content = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        (content is "first" or "second").ShouldBeTrue();
    }

    private OperatingSystemWorkspaceHost CreateFileSystemOperatingSystemWorkspaceHostEdit(ISecurityGrantStore? grantStore = null) => WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostEdit }, grantStore ?? TestSecurity.GrantStore(), TimeProvider.System);

    private static AtomicFileReplaceRequest ReplaceRequest(string path, ContentHash expected, ImmutableArray<byte> content, WorkspaceMutationId id) => new(id, new FileSystemPath(path), expected, content, TestSecurity.Grant());

    private static WorkspaceMutationId MutationId(int suffix) => new(Guid.Parse($"10000000-0000-0000-0000-{suffix:D12}"));

    private readonly string _rootOperatingSystemWorkspaceHostPatch = Path.Combine(Path.GetTempPath(), $"agentkit-patch-{Guid.NewGuid():N}");

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateIsValid_CommitsAtomicallyWithExactSecurityBinding()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(store);
        var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(1), new FileSystemPath("created.txt"), "hello\r\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.AtomicCommitted);
        result.Entries.Single().Status.ShouldBe(WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllBytesAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "created.txt"), TestContext.Current.CancellationToken)).ShouldBe(entry.Content);
        File.GetUnixFileMode(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "created.txt")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostPatch, ".agentkit-stage-*").ShouldBeEmpty();
        var enforcement = store.Enforcements.Single();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe(WorkspacePatchSecurityBinding.CreateResources(entry.Id, entry.Path));
        enforcement.InputFingerprint.ShouldBe(WorkspacePatchSecurityBinding.CreateFingerprint(entry.Id, entry.Path, entry.Content));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMixedPlanIsValid_CommitsInSourceOrderWithHonestVisibilityStatus()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        await WriteAsync("replace.sh", "old");
        await WriteAsync("delete.txt", "delete");
        await WriteAsync("move.txt", "move");
        File.SetUnixFileMode(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "replace.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var replaceExpected = await FingerprintAsync("replace.sh");
        var deleteExpected = await FingerprintAsync("delete.txt");
        var moveExpected = await FingerprintAsync("move.txt");
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(store);
        var create = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(2), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var replace = new WorkspacePatchReplace(MutationIdOperatingSystemWorkspaceHostPatch(3), new FileSystemPath("replace.sh"), replaceExpected, "updated\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var delete = new WorkspacePatchDelete(MutationIdOperatingSystemWorkspaceHostPatch(4), new FileSystemPath("delete.txt"), deleteExpected, TestSecurity.Grant());
        var move = new WorkspacePatchMove(MutationIdOperatingSystemWorkspaceHostPatch(5), new FileSystemPath("move.txt"), new FileSystemPath("moved.txt"), moveExpected, TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([create, replace, delete, move]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.CommittedWithNonAtomicVisibility);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "new.txt"), TestContext.Current.CancellationToken)).ShouldBe("new");
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "replace.sh"), TestContext.Current.CancellationToken)).ShouldBe("updated\n");
        File.GetUnixFileMode(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "replace.sh")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "delete.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "move.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "moved.txt"), TestContext.Current.CancellationToken)).ShouldBe("move");
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostPatch, ".agentkit-stage-*").ShouldBeEmpty();
        store.Enforcements.Select(static item => item.Effect).ShouldBe([SecurityEffect.Create, SecurityEffect.Replace, SecurityEffect.Delete, SecurityEffect.Move,]);
        store.Enforcements[1].Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(replace.Id, replace.Path));
        store.Enforcements[1].InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(replace.Id, replace.Path, replace.ExpectedContentFingerprint, replace.Content));
        store.Enforcements[2].Resources.ShouldBe(WorkspacePatchSecurityBinding.DeleteResources(delete.Path));
        store.Enforcements[3].Resources.ShouldBe(WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterPreconditionIsStale_RejectsWholePlanBeforeEffects()
    {
        await WriteAsync("existing.txt", "current");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var create = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(6), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var stale = new WorkspacePatchReplace(MutationIdOperatingSystemWorkspaceHostPatch(7), new FileSystemPath("existing.txt"), new ContentHash("sha256:stale"), "changed"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([create, stale]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Unchanged);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "new.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "existing.txt"), TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostPatch, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterGrantIsDenied_ObservesAndMutatesNothing()
    {
        var store = new RecordingGrantStore(deniedIndex: 1);
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(store);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(8), new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()), new WorkspacePatchDelete(MutationIdOperatingSystemWorkspaceHostPatch(9), new FileSystemPath("secret-missing.txt"), new ContentHash("sha256:any"), TestSecurity.Grant()));
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.Count.ShouldBe(2);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "first.txt")).ShouldBeFalse();
        Directory.GetFiles(_rootOperatingSystemWorkspaceHostPatch, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPathsOverlap_RejectsBeforeGrantConsumption()
    {
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(store);
        var path = new FileSystemPath("same.txt");
        var request = new WorkspacePatchRequest([new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(10), path, "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()), new WorkspacePatchDelete(MutationIdOperatingSystemWorkspaceHostPatch(11), path, new ContentHash("sha256:any"), TestSecurity.Grant()),]);
        var result = await fileSystem.ApplyPatchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.ShouldBeEmpty();
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "same.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateTargetExists_DoesNotReplaceIt()
    {
        await WriteAsync("existing.txt", "original");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(12), new FileSystemPath("existing.txt"), "replacement"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "existing.txt"), TestContext.Current.CancellationToken)).ShouldBe("original");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenEntryCountExceedsConfiguredBoundary_RejectsBeforeGrantConsumption()
    {
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostPatch, MaximumPatchEntries = 1 },
            new RecordingGrantStore(),
            TimeProvider.System);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(20), new FileSystemPath("one.txt"), "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(21), new FileSystemPath("two.txt"), "two"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("entry boundary");
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "one.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenEntryKindDoesNotMatchItsConcreteContract_RejectsBeforeGrantConsumption()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var mismatched = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(22), new FileSystemPath("mismatched.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant())
            with
        { Kind = WorkspacePatchEntryKind.Delete };
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([mismatched]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("does not match its concrete contract");
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "mismatched.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenTwoEntriesShareTheSameMutationIdentity_RejectsBeforeGrantConsumption()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var sharedId = MutationIdOperatingSystemWorkspaceHostPatch(23);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(sharedId, new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(sharedId, new FileSystemPath("second.txt"), "second"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("unique");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenSingleEntryContentExceedsWriteBoundary_RejectsBeforeGrantConsumption()
    {
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostPatch, MaximumWriteBytes = 2 },
            new RecordingGrantStore(),
            TimeProvider.System);
        var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(24), new FileSystemPath("too-big.txt"), "too long"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("write boundary");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenAggregateContentExceedsPatchByteBoundary_RejectsBeforeGrantConsumption()
    {
        var fileSystem = WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostPatch, MaximumPatchBytes = 5 },
            new RecordingGrantStore(),
            TimeProvider.System);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(25), new FileSystemPath("one.txt"), "abc"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(26), new FileSystemPath("two.txt"), "abc"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("aggregate byte boundary");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenSourceParentDirectoryIsMissing_RejectsAtPreflight()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(27), new FileSystemPath("missing-parent/new.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("does not exist");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenSourceParentIsSymlinkOutsideRoot_RejectsAtPreflightAsBoundary()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-patch-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "outside-link"), outside);
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
            var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(28), new FileSystemPath("outside-link/new.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
            var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
            result.SafeMessage.ShouldNotBeNull().ShouldContain("inaccessible boundary");
            File.Exists(Path.Combine(outside, "new.txt")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenDeleteTargetVersionChanged_RejectsAtPreflight()
    {
        await WriteAsync("deletable.txt", "current");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchDelete(MutationIdOperatingSystemWorkspaceHostPatch(29), new FileSystemPath("deletable.txt"), new ContentHash("sha256:stale"), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "deletable.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMoveSourceVersionChanged_RejectsAtPreflight()
    {
        await WriteAsync("movable.txt", "current");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchMove(MutationIdOperatingSystemWorkspaceHostPatch(30), new FileSystemPath("movable.txt"), new FileSystemPath("moved.txt"), new ContentHash("sha256:stale"), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "movable.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "moved.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMoveDestinationParentIsMissing_RejectsAtPreflight()
    {
        await WriteAsync("movable.txt", "current");
        var expected = await FingerprintAsync("movable.txt");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchMove(MutationIdOperatingSystemWorkspaceHostPatch(31), new FileSystemPath("movable.txt"), new FileSystemPath("missing-dir/moved.txt"), expected, TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "movable.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMoveDestinationAlreadyExists_RejectsAtPreflight()
    {
        await WriteAsync("movable.txt", "current");
        await WriteAsync("moved.txt", "occupied");
        var expected = await FingerprintAsync("movable.txt");
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchMove(MutationIdOperatingSystemWorkspaceHostPatch(32), new FileSystemPath("movable.txt"), new FileSystemPath("moved.txt"), expected, TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        (await File.ReadAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "moved.txt"), TestContext.Current.CancellationToken)).ShouldBe("occupied");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenRequiredFileIsMissing_RejectsAtPreflightWithoutRevealingDetails()
    {
        var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchDelete(MutationIdOperatingSystemWorkspaceHostPatch(33), new FileSystemPath("never-existed.txt"), new ContentHash("sha256:any"), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("does not exist");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenParentDirectoryIsNotWritable_RejectsStagingWithoutPartialEffect()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var readOnlyDirectory = Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "readonly-dir");
        _ = Directory.CreateDirectory(readOnlyDirectory);
        File.SetUnixFileMode(readOnlyDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
            var entry = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(34), new FileSystemPath("readonly-dir/new.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
            var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
            result.SafeMessage.ShouldNotBeNull().ShouldContain("staged");
        }
        finally
        {
            File.SetUnixFileMode(readOnlyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenALaterEntryFailsToStage_RemovesTheEarlierEntrysOrphanedStagingFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var readOnlyDirectory = Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "readonly-dir-2");
        _ = Directory.CreateDirectory(readOnlyDirectory);
        File.SetUnixFileMode(readOnlyDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var fileSystem = CreateFileSystemOperatingSystemWorkspaceHostPatch(new RecordingGrantStore());
            var stagesFine = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(35), new FileSystemPath("stages-fine.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
            var failsToStage = new WorkspacePatchCreate(MutationIdOperatingSystemWorkspaceHostPatch(36), new FileSystemPath("readonly-dir-2/new.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
            var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([stagesFine, failsToStage]), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
            File.Exists(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, "stages-fine.txt")).ShouldBeFalse();
            Directory.GetFiles(_rootOperatingSystemWorkspaceHostPatch, ".agentkit-stage-*").ShouldBeEmpty();
        }
        finally
        {
            File.SetUnixFileMode(readOnlyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private OperatingSystemWorkspaceHost CreateFileSystemOperatingSystemWorkspaceHostPatch(ISecurityGrantStore store) => WorkspaceHostTestFactory.Create(new WorkspaceHostOptions { RootDirectory = _rootOperatingSystemWorkspaceHostPatch }, store, TimeProvider.System);

    private async Task WriteAsync(string path, string content) => await File.WriteAllTextAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, path), content, TestContext.Current.CancellationToken);

    private async Task<ContentHash> FingerprintAsync(string path) => FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(Path.Combine(_rootOperatingSystemWorkspaceHostPatch, path), TestContext.Current.CancellationToken));

    private static WorkspaceMutationId MutationIdOperatingSystemWorkspaceHostPatch(int suffix) => new(Guid.Parse($"20000000-0000-0000-0000-{suffix:D12}"));

    private sealed class RecordingGrantStore(int? deniedIndex = null): ISecurityGrantStore
    {
        public List<SecurityEnforcementRequest> Enforcements { get; } = [];

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Enforcements.Add(enforcement);
            var denied = Enforcements.Count - 1 == deniedIndex;
            return ValueTask.FromResult(new GrantConsumptionResult(denied ? GrantConsumptionStatus.Unknown : GrantConsumptionStatus.Consumed, 0, denied ? "Denied by test store." : "Consumed by test store.", denied ? null : new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement, intent.RequiredFence, SecurityEnforcementBinding.Fingerprint(enforcement, intent), DateTimeOffset.UnixEpoch)));
        }

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }

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

        Directory.Delete(_rootOperatingSystemWorkspaceHostSearch, recursive: true);
        GC.SuppressFinalize(this);
        Directory.Delete(_rootOperatingSystemWorkspaceHostEdit, recursive: true);
        GC.SuppressFinalize(this);
        Directory.Delete(_rootOperatingSystemWorkspaceHostPatch, recursive: true);
        GC.SuppressFinalize(this);
    }
}
