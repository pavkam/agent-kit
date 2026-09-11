// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;



/// <summary>Verifies SandboxedFileSystem behavior and contracts.</summary>
public sealed class SandboxedFileSystemTests: IDisposable
{
    public SandboxedFileSystemTests()
    {
        _ = Directory.CreateDirectory(_rootSandboxedFileSystemSearch);
        _ = Directory.CreateDirectory(_rootSandboxedFileSystemEdit);
        _ = Directory.CreateDirectory(_rootSandboxedFileSystemPatch);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentkit-fs-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outsideRoot = Path.Combine(Path.GetTempPath(), "agentkit-fs-outside-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SandboxedFileSystem(null!, TestSecurity.GrantStore(), TimeProvider.System));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenRootDirectoryNotRooted_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "relative/path" });
        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(options, TestSecurity.GrantStore(), TimeProvider.System));
    }

    [Fact]
    public void Constructor_WhenRootDirectoryBlank_ThrowsArgumentException()
    {
        var options = Options.Create(new SandboxedFileSystemOptions { RootDirectory = "   " });
        _ = Should.Throw<ArgumentException>(() => new SandboxedFileSystem(options, TestSecurity.GrantStore(), TimeProvider.System));
    }

    [Fact]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility()
    {
        _ = Directory.CreateDirectory(_root);
        _ = new SandboxedFileSystem(Options.Create(new SandboxedFileSystemOptions { RootDirectory = _root }), TestSecurity.GrantStore(), TimeProvider.System, null);
    }

    [Fact]
    public void Constructor_WhenIntentIdsNull_ThrowsWithExactParameterName()
    {
        _ = Directory.CreateDirectory(_root);
        var exception = Should.Throw<ArgumentNullException>(() => new SandboxedFileSystem(Options.Create(new SandboxedFileSystemOptions { RootDirectory = _root }), TestSecurity.GrantStore(), TimeProvider.System, null, null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task ReadAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var fs = CreateFileSystem();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => fs.ReadAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var fs = CreateFileSystem();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => fs.WriteAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenFileDoesNotExist_CreatesFileAndReturnsWritten()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("notes.txt"), "hello world", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant());
        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);
        var written = result.ShouldBeOfType<FileWritten>();
        written.BytesWritten.ShouldBe(11L);
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("hello world");
    }

    [Fact]
    public async Task WriteAsync_WhenParentDirectoryMissing_DoesNotCreateParentDirectory()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("sub/dir/notes.txt"), "nested", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant());
        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWriteFailed>();
        Directory.Exists(Path.Combine(_root, "sub")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenStoreReconcilesAnEarlierIntent_DoesNotCreateTheTarget()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Reconciled, 0, "Reconciled."),
        };
        var fs = CreateFileSystem(grantStore: store);
        var path = new FileSystemPath("reconciled.txt");
        var result = await fs.WriteAsync(new FileWriteRequest(path, "protected", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.Exists(Path.Combine(_root, path.Value)).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotCreateTheTarget()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestSecurity.RecordingGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
        var fs = CreateFileSystem(grantStore: store);
        var path = new FileSystemPath("cancelled-before-write.txt");
        var action = async () => await fs.WriteAsync(new FileWriteRequest(path, "protected", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        File.Exists(Path.Combine(_root, path.Value)).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        const string text = "captured";
        var store = new InMemorySecurityGrantStore(TimeProvider.System);
        var fs = CreateFileSystem(grantStore: store);
        var path = new FileSystemPath("captured-write.txt");
        var grant = TestSecurity.CapturedGrant(fs.SecurityAudience, SecurityOperationKind.FileWrite, FileSecurityBinding.WriteEffect(FileWriteMode.CreateOrOverwrite), [FileSecurityBinding.Resource(path)], FileSecurityBinding.WriteFingerprint(path, text, FileWriteMode.CreateOrOverwrite));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await fs.WriteAsync(new FileWriteRequest(path, text, FileWriteMode.CreateOrOverwrite, grant), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, path.Value)).ShouldBe(text);
    }

    [Fact]
    public async Task ReadAsync_WhenFileExists_ReturnsContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "existing content");
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        var read = result.ShouldBeOfType<FileRead>();
        read.Content.ShouldBe("existing content");
        read.Bytes.ShouldBe(16L);
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExist_ReturnsFileNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("missing.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
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

        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("possibly-secret.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldBe("Grant does not match.");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Resources.ShouldBe([FileSecurityBinding.Resource(new FileSystemPath("possibly-secret.txt"))]);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.ReadFingerprint(new FileSystemPath("possibly-secret.txt")));
    }

    [Fact]
    public async Task ReadAsync_WhenConsumedReceiptIsMissing_DoesNotReadTheTarget()
    {
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "receipt-missing.txt"), "protected");
        var store = new TestSecurity.RecordingGrantStore
        {
            IncludeIntentReceipt = false
        };
        var fs = CreateFileSystem(grantStore: store);
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("receipt-missing.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ReadAsync_WhenReceiptReferencesAnotherIntent_DoesNotReadTheTarget()
    {
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "receipt-mismatch.txt"), "protected");
        var store = new TestSecurity.RecordingGrantStore
        {
            ReturnExactIntentReceipt = false
        };
        var fs = CreateFileSystem(grantStore: store);
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("receipt-mismatch.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ReadAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        const string path = "captured-read.txt";
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, path), "captured");
        var store = new InMemorySecurityGrantStore(TimeProvider.System);
        var fs = CreateFileSystem(grantStore: store);
        var filePath = new FileSystemPath(path);
        var grant = TestSecurity.CapturedGrant(fs.SecurityAudience, SecurityOperationKind.FileRead, SecurityEffect.Observe, [FileSecurityBinding.Resource(filePath)], FileSecurityBinding.ReadFingerprint(filePath));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await fs.ReadAsync(new FileReadRequest(filePath, grant), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileRead>().Content.ShouldBe("captured");
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
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("outside-link/secret.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("secret-link.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("outside-link/created.txt"), "must stay inside", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("secret-link.txt"), "must stay inside", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWriteDenied>();
        File.ReadAllText(outsidePath).ShouldBe("outside");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateNewAndFileExists_ReturnsFileAlreadyExists()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "already here");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "new content", FileWriteMode.CreateNew, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileAlreadyExists>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("already here");
    }

    [Fact]
    public async Task WriteAsync_WhenConcurrentCreateNewTargetsSamePath_CreatesExactlyOnce()
    {
        var fs = CreateFileSystem();
        var first = new FileWriteRequest(new FileSystemPath("notes.txt"), "first", FileWriteMode.CreateNew, TestSecurity.Grant());
        var second = new FileWriteRequest(new FileSystemPath("notes.txt"), "second", FileWriteMode.CreateNew, TestSecurity.Grant());
        var results = await Task.WhenAll(fs.WriteAsync(first, TestContext.Current.CancellationToken), fs.WriteAsync(second, TestContext.Current.CancellationToken));
        results.Count(static result => result is FileWritten).ShouldBe(1);
        results.Count(static result => result is FileAlreadyExists).ShouldBe(1);
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBeOneOf("first", "second");
    }

    [Fact]
    public async Task WriteAsync_WhenRequestWasMutatedToUndefinedMode_ThrowsBeforeEffects()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("notes.txt"), "content", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()) with
        {
            Mode = (FileWriteMode) int.MaxValue
        };
        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => fs.WriteAsync(request, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request.Mode");
        File.Exists(Path.Combine(_root, "notes.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenModeAppend_AppendsToExistingContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "first-");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "second", FileWriteMode.Append, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("first-second");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateOrOverwrite_ReplacesExistingContent()
    {
        var fs = CreateFileSystem();
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "old content that is longer");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "new", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("new");
    }

    [Fact]
    public async Task ReadAsync_WhenFileExceedsMaximumReadBytes_ReturnsFileReadDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumReadBytes = 4);
        _ = Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "this is too long");
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileReadDenied>();
    }

    [Fact]
    public async Task WriteAsync_WhenContentExceedsMaximumWriteBytes_ReturnsFileWriteDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 4);
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "this is too long", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileWriteDenied>().SafeMessage.ShouldBe("Grant is revoked.");
        File.ReadAllText(Path.Combine(_root, "notes.txt")).ShouldBe("original");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.WriteFingerprint(new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite));
    }

    [Fact]
    public async Task ReadAsync_WhenRootDirectoryDoesNotExist_ReturnsFileNotFound()
    {
        var options = new SandboxedFileSystemOptions
        {
            RootDirectory = _root
        };
        var fs = new SandboxedFileSystem(Options.Create(options), TestSecurity.GrantStore(), TimeProvider.System);
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExistInExistingRoot_ReturnsFileNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("file.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileNotFound>();
    }

    [Fact]
    public async Task EnumerateAsync_WhenRootContainsEntries_ReturnsOrdinalPagesWithStableCursor()
    {
        var fs = CreateFileSystem();
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        _ = Directory.CreateDirectory(Path.Combine(_root, "c"));
        var first = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 2, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        var second = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 2, first.Continuation, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var first = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(_root, "c.txt"), "c");
        var resumed = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, first.Continuation, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(new FileSystemPath("outside"), 10, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(path, 7, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
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
    public async Task GlobAsync_WhenGrantDenied_DoesNotObserveAndUsesExactBinding()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Revoked, 1, "Grant is revoked."),
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
                if (activity.OperationName == AgentKitActivityNames.FileSystemOperation && Equals(activity.GetTagItem(AgentKitTagNames.SecurityRequestId), request.Grant.RequestId.ToString()))
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

    private SandboxedFileSystem CreateFileSystem(Action<SandboxedFileSystemOptions>? configure = null, ISecurityGrantStore? grantStore = null)
    {
        _ = Directory.CreateDirectory(_root);
        var options = new SandboxedFileSystemOptions
        {
            RootDirectory = _root
        };
        configure?.Invoke(options);
        return new SandboxedFileSystem(Options.Create(options), grantStore ?? TestSecurity.GrantStore(), TimeProvider.System);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    private readonly string _rootSandboxedFileSystemSearch = Path.Combine(Path.GetTempPath(), $"agentkit-search-{Guid.NewGuid():N}");
    [Fact]
    public async Task SearchAsync_WhenLiteralMatches_ReturnsDeterministicVersionedByteLocations()
    {
        _ = Directory.CreateDirectory(Path.Combine(_rootSandboxedFileSystemSearch, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "src", "b.cs"), "first\nNeedle café\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "src", "a.cs"), "needle\n", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
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
        _ = Directory.CreateDirectory(Path.Combine(_rootSandboxedFileSystemSearch, "src"));
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "src", "code.cs"), "item-42 item-x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "src", "notes.md"), "item-99", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern( /*lang=regex*/"item-[0-9]+", FileSearchPatternKind.RegularExpression), pathPattern: new GlobPattern("**/*.cs")), TestContext.Current.CancellationToken);
        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/code.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenFilesAreHiddenBinaryOrInvalidUtf8_ExcludesThemWithoutDecoding()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, ".hidden"), "needle", TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_rootSandboxedFileSystemSearch, "binary"), "needle\0tail"u8.ToArray(), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_rootSandboxedFileSystemSearch, "invalid"), [0x6e, 0x65, 0x65, 0x64, 0x6c, 0x65, 0xff], TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
        result.VisitedFiles.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchLimitReached_ReturnsTypedPartialResult()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "a.txt"), "needle needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenRetainedMatchesExactlyEqualLimit_RemainsComplete()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "a.txt"), "one needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenMatchingLineIsLong_RetainsBoundedContextContainingMatch()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "a.txt"), $"{new string('x', 200)}needle{new string('y', 200)}", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 1024 * 1024, 100, 30, TimeSpan.FromSeconds(10), TestSecurity.Grant());
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
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch(configure: static options => options.MaximumSearchMatches = 1);
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
            _ = Directory.CreateSymbolicLink(Path.Combine(_rootSandboxedFileSystemSearch, "linked"), outside);
            var fileSystem = CreateFileSystemSandboxedFileSystemSearch();
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
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch(grantStore);
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
        await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemSearch, "a.txt"), "needle", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemSearch(timeProvider: new AdvancingTimeProvider());
        var result = await fileSystem.SearchAsync(Request(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumDuration: TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.TimedOut);
        result.Complete.ShouldBeFalse();
    }

    private SandboxedFileSystem CreateFileSystemSandboxedFileSystemSearch(ISecurityGrantStore? grantStore = null, TimeProvider? timeProvider = null, Action<SandboxedFileSystemOptions>? configure = null)
    {
        var options = new SandboxedFileSystemOptions
        {
            RootDirectory = _rootSandboxedFileSystemSearch
        };
        configure?.Invoke(options);
        return new SandboxedFileSystem(Options.Create(options), grantStore ?? TestSecurity.GrantStore(), timeProvider ?? TimeProvider.System);
    }

    private static FileSearchRequest Request(FileSearchPattern pattern, FileSystemPath? basePath = null, GlobPattern? pathPattern = null, bool caseSensitive = true, int maximumMatches = 100, TimeSpan? maximumDuration = null) => new(basePath, pattern, pathPattern ?? new GlobPattern("**/*"), caseSensitive, includeHidden: false, maximumDepth: 10, maximumFiles: 100, maximumBytes: 1024 * 1024, maximumMatches, maximumLineBytes: 1024, maximumDuration ?? TimeSpan.FromSeconds(10), TestSecurity.Grant());
    private sealed class AdvancingTimeProvider: TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Increment(ref _timestamp);
    }

    private readonly string _rootSandboxedFileSystemEdit = Path.Combine(Path.GetTempPath(), $"agentkit-edit-{Guid.NewGuid():N}");
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
        await File.WriteAllBytesAsync(Path.Combine(_rootSandboxedFileSystemEdit, "a.txt"), bytes, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit(grantStore);
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
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit(grantStore);
        var result = await fileSystem.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionMatches_CommitsAtomicallyAndPreservesMode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var target = Path.Combine(_rootSandboxedFileSystemEdit, "script.sh");
        await File.WriteAllTextAsync(target, "old\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var original = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var replacement = "new\r\n"u8.ToArray().ToImmutableArray();
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit(grantStore);
        var request = ReplaceRequest("script.sh", FileSecurityBinding.ContentFingerprint(original), replacement, MutationId(1));
        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(replacement.AsSpan()));
        (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)).ShouldBe(replacement);
        File.GetUnixFileMode(target).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.GetFiles(_rootSandboxedFileSystemEdit, ".agentkit-stage-*").ShouldBeEmpty();
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Replace);
        enforcement.Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path));
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionChanged_ReturnsConflictWithoutStaging()
    {
        var target = Path.Combine(_rootSandboxedFileSystemEdit, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit();
        var request = ReplaceRequest("a.txt", new ContentHash("sha256:stale"), "replacement"u8.ToArray().ToImmutableArray(), MutationId(2));
        var result = await fileSystem.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Conflict);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootSandboxedFileSystemEdit, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenGrantDenied_CreatesNoStagingFile()
    {
        var target = Path.Combine(_rootSandboxedFileSystemEdit, "a.txt");
        await File.WriteAllTextAsync(target, "current", TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit(grantStore);
        var result = await fileSystem.ReplaceAsync(ReplaceRequest("a.txt", FileSecurityBinding.ContentFingerprint(bytes), "replacement"u8.ToArray().ToImmutableArray(), MutationId(3)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootSandboxedFileSystemEdit, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenTwoPlansRace_OnlyOneExpectedVersionCommits()
    {
        var target = Path.Combine(_rootSandboxedFileSystemEdit, "a.txt");
        await File.WriteAllTextAsync(target, "old", TestContext.Current.CancellationToken);
        var expected = FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
        var fileSystem = CreateFileSystemSandboxedFileSystemEdit();
        var first = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "first"u8.ToArray().ToImmutableArray(), MutationId(4)), TestContext.Current.CancellationToken).AsTask();
        var second = fileSystem.ReplaceAsync(ReplaceRequest("a.txt", expected, "second"u8.ToArray().ToImmutableArray(), MutationId(5)), TestContext.Current.CancellationToken).AsTask();
        var results = await Task.WhenAll(first, second);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Committed).ShouldBe(1);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Conflict).ShouldBe(1);
        var content = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        (content is "first" or "second").ShouldBeTrue();
    }

    private SandboxedFileSystem CreateFileSystemSandboxedFileSystemEdit(ISecurityGrantStore? grantStore = null) => new(Options.Create(new SandboxedFileSystemOptions { RootDirectory = _rootSandboxedFileSystemEdit }), grantStore ?? TestSecurity.GrantStore(), TimeProvider.System);
    private static AtomicFileReplaceRequest ReplaceRequest(string path, ContentHash expected, ImmutableArray<byte> content, WorkspaceMutationId id) => new(id, new FileSystemPath(path), expected, content, TestSecurity.Grant());
    private static WorkspaceMutationId MutationId(int suffix) => new(Guid.Parse($"10000000-0000-0000-0000-{suffix:D12}"));
    private readonly string _rootSandboxedFileSystemPatch = Path.Combine(Path.GetTempPath(), $"agentkit-patch-{Guid.NewGuid():N}");
    [Fact]
    public async Task ApplyPatchAsync_WhenCreateIsValid_CommitsAtomicallyWithExactSecurityBinding()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(store);
        var entry = new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(1), new FileSystemPath("created.txt"), "hello\r\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.AtomicCommitted);
        result.Entries.Single().Status.ShouldBe(WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllBytesAsync(Path.Combine(_rootSandboxedFileSystemPatch, "created.txt"), TestContext.Current.CancellationToken)).ShouldBe(entry.Content);
        File.GetUnixFileMode(Path.Combine(_rootSandboxedFileSystemPatch, "created.txt")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(_rootSandboxedFileSystemPatch, ".agentkit-stage-*").ShouldBeEmpty();
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
        File.SetUnixFileMode(Path.Combine(_rootSandboxedFileSystemPatch, "replace.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var replaceExpected = await FingerprintAsync("replace.sh");
        var deleteExpected = await FingerprintAsync("delete.txt");
        var moveExpected = await FingerprintAsync("move.txt");
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(store);
        var create = new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(2), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var replace = new WorkspacePatchReplace(MutationIdSandboxedFileSystemPatch(3), new FileSystemPath("replace.sh"), replaceExpected, "updated\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var delete = new WorkspacePatchDelete(MutationIdSandboxedFileSystemPatch(4), new FileSystemPath("delete.txt"), deleteExpected, TestSecurity.Grant());
        var move = new WorkspacePatchMove(MutationIdSandboxedFileSystemPatch(5), new FileSystemPath("move.txt"), new FileSystemPath("moved.txt"), moveExpected, TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([create, replace, delete, move]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.CommittedWithNonAtomicVisibility);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Committed);
        (await File.ReadAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, "new.txt"), TestContext.Current.CancellationToken)).ShouldBe("new");
        (await File.ReadAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, "replace.sh"), TestContext.Current.CancellationToken)).ShouldBe("updated\n");
        File.GetUnixFileMode(Path.Combine(_rootSandboxedFileSystemPatch, "replace.sh")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Exists(Path.Combine(_rootSandboxedFileSystemPatch, "delete.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_rootSandboxedFileSystemPatch, "move.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, "moved.txt"), TestContext.Current.CancellationToken)).ShouldBe("move");
        Directory.GetFiles(_rootSandboxedFileSystemPatch, ".agentkit-stage-*").ShouldBeEmpty();
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
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(new RecordingGrantStore());
        var create = new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(6), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var stale = new WorkspacePatchReplace(MutationIdSandboxedFileSystemPatch(7), new FileSystemPath("existing.txt"), new ContentHash("sha256:stale"), "changed"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([create, stale]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Unchanged);
        File.Exists(Path.Combine(_rootSandboxedFileSystemPatch, "new.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, "existing.txt"), TestContext.Current.CancellationToken)).ShouldBe("current");
        Directory.GetFiles(_rootSandboxedFileSystemPatch, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterGrantIsDenied_ObservesAndMutatesNothing()
    {
        var store = new RecordingGrantStore(deniedIndex: 1);
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(store);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(8), new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()), new WorkspacePatchDelete(MutationIdSandboxedFileSystemPatch(9), new FileSystemPath("secret-missing.txt"), new ContentHash("sha256:any"), TestSecurity.Grant()));
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.Count.ShouldBe(2);
        File.Exists(Path.Combine(_rootSandboxedFileSystemPatch, "first.txt")).ShouldBeFalse();
        Directory.GetFiles(_rootSandboxedFileSystemPatch, ".agentkit-stage-*").ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPathsOverlap_RejectsBeforeGrantConsumption()
    {
        var store = new RecordingGrantStore();
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(store);
        var path = new FileSystemPath("same.txt");
        var request = new WorkspacePatchRequest([new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(10), path, "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()), new WorkspacePatchDelete(MutationIdSandboxedFileSystemPatch(11), path, new ContentHash("sha256:any"), TestSecurity.Grant()),]);
        var result = await fileSystem.ApplyPatchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.ShouldBeEmpty();
        File.Exists(Path.Combine(_rootSandboxedFileSystemPatch, "same.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateTargetExists_DoesNotReplaceIt()
    {
        await WriteAsync("existing.txt", "original");
        var fileSystem = CreateFileSystemSandboxedFileSystemPatch(new RecordingGrantStore());
        var entry = new WorkspacePatchCreate(MutationIdSandboxedFileSystemPatch(12), new FileSystemPath("existing.txt"), "replacement"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fileSystem.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        (await File.ReadAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, "existing.txt"), TestContext.Current.CancellationToken)).ShouldBe("original");
    }

    private SandboxedFileSystem CreateFileSystemSandboxedFileSystemPatch(ISecurityGrantStore store) => new(Options.Create(new SandboxedFileSystemOptions { RootDirectory = _rootSandboxedFileSystemPatch }), store, TimeProvider.System);
    private async Task WriteAsync(string path, string content) => await File.WriteAllTextAsync(Path.Combine(_rootSandboxedFileSystemPatch, path), content, TestContext.Current.CancellationToken);
    private async Task<ContentHash> FingerprintAsync(string path) => FileSecurityBinding.ContentFingerprint(await File.ReadAllBytesAsync(Path.Combine(_rootSandboxedFileSystemPatch, path), TestContext.Current.CancellationToken));
    private static WorkspaceMutationId MutationIdSandboxedFileSystemPatch(int suffix) => new(Guid.Parse($"20000000-0000-0000-0000-{suffix:D12}"));
    private sealed class RecordingGrantStore(int? deniedIndex = null): ISecurityGrantStore
    {
        public List<SecurityEnforcementRequest> Enforcements { get; } = [];

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default)
        {
            Enforcements.Add(enforcement);
            var denied = Enforcements.Count - 1 == deniedIndex;
            return ValueTask.FromResult(new GrantConsumptionResult(denied ? GrantConsumptionStatus.Unknown : GrantConsumptionStatus.Consumed, 0, denied ? "Denied by test store." : "Consumed by test store."));
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Enforcements.Add(enforcement);
            var denied = Enforcements.Count - 1 == deniedIndex;
            return ValueTask.FromResult(new GrantConsumptionResult(denied ? GrantConsumptionStatus.Unknown : GrantConsumptionStatus.Consumed, 0, denied ? "Denied by test store." : "Consumed by test store.", denied ? null : new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement, intent.RequiredFence, SecurityEnforcementBinding.Fingerprint(enforcement, intent), DateTimeOffset.UnixEpoch)));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
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

        Directory.Delete(_rootSandboxedFileSystemSearch, recursive: true);
        GC.SuppressFinalize(this);
        Directory.Delete(_rootSandboxedFileSystemEdit, recursive: true);
        GC.SuppressFinalize(this);
        Directory.Delete(_rootSandboxedFileSystemPatch, recursive: true);
        GC.SuppressFinalize(this);
    }
}
