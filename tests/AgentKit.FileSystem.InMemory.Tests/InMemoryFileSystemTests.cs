// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

/// <summary>Verifies InMemoryFileSystem behavior and contracts.</summary>
public sealed class InMemoryFileSystemTests
{
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new InMemoryFileSystem(null!, TestSecurity.GrantStore(), TimeProvider.System));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenIntentIdsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemoryFileSystem(
            Options.Create(new InMemoryFileSystemOptions()), TestSecurity.GrantStore(), TimeProvider.System, null, null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenLoggerArgumentIsNull_UsesNullLogger() =>
        _ = new InMemoryFileSystem(Options.Create(new InMemoryFileSystemOptions()), TestSecurity.GrantStore(), TimeProvider.System, null);

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
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("hello world");
    }

    [Fact]
    public async Task WriteAsync_WhenParentDirectoryMissing_DoesNotCreateParentDirectory()
    {
        var fs = CreateFileSystem();
        var request = new FileWriteRequest(new FileSystemPath("sub/dir/notes.txt"), "nested", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant());
        var result = await fs.WriteAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWriteFailed>();
        var enumeration = await fs.EnumerateAsync(new DirectoryEnumerationRequest(new FileSystemPath("sub"), 10, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        enumeration.Status.ShouldBe(DirectoryEnumerationStatus.NotFound);
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
        fs.TryReadAllBytes(path, out _).ShouldBeFalse();
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
        fs.TryReadAllBytes(path, out _).ShouldBeFalse();
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
        fs.TryReadAllBytes(path, out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe(text);
    }

    [Fact]
    public async Task ReadAsync_WhenFileExists_ReturnsContent()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("notes.txt"), "existing content");
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
            fs.Seed(new FileSystemPath("possibly-secret.txt"), "secret");
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
        var store = new TestSecurity.RecordingGrantStore
        {
            IncludeIntentReceipt = false
        };
        var fs = CreateFileSystem(grantStore: store);
        fs.Seed(new FileSystemPath("receipt-missing.txt"), "protected");
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("receipt-missing.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ReadAsync_WhenReceiptReferencesAnotherIntent_DoesNotReadTheTarget()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            ReturnExactIntentReceipt = false
        };
        var fs = CreateFileSystem(grantStore: store);
        fs.Seed(new FileSystemPath("receipt-mismatch.txt"), "protected");
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("receipt-mismatch.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileReadDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ReadAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        const string path = "captured-read.txt";
        var store = new InMemorySecurityGrantStore(TimeProvider.System);
        var fs = CreateFileSystem(grantStore: store);
        fs.Seed(new FileSystemPath(path), "captured");
        var filePath = new FileSystemPath(path);
        var grant = TestSecurity.CapturedGrant(fs.SecurityAudience, SecurityOperationKind.FileRead, SecurityEffect.Observe, [FileSecurityBinding.Resource(filePath)], FileSecurityBinding.ReadFingerprint(filePath));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await fs.ReadAsync(new FileReadRequest(filePath, grant), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileRead>().Content.ShouldBe("captured");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateNewAndFileExists_ReturnsFileAlreadyExists()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("notes.txt"), "already here");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "new content", FileWriteMode.CreateNew, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileAlreadyExists>();
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("already here");
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
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBeOneOf("first", "second");
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
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenModeAppendAndTargetIsMissing_DoesNotCreateTheFile()
    {
        // file-system-access-and-bounds.md write-disposition table: Append + missing target => "Not found, no mutation".
        var fs = CreateFileSystem();

        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("absent.log"), "line", FileWriteMode.Append, TestSecurity.Grant()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWriteFailed>();
        fs.TryReadAllBytes(new FileSystemPath("absent.log"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenModeAppend_AppendsToExistingContent()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("notes.txt"), "first-");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "second", FileWriteMode.Append, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("first-second");
    }

    [Fact]
    public async Task WriteAsync_WhenModeCreateOrOverwrite_ReplacesExistingContent()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("notes.txt"), "old content that is longer");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "new", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("new");
    }

    [Fact]
    public async Task WriteAsync_WhenModeReplaceExistingAndFileExists_ReplacesExactContent()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("notes.txt"), "old content");

        var result = await fs.WriteAsync(new FileWriteRequest(
            new FileSystemPath("notes.txt"), " \t", FileWriteMode.ReplaceExisting, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<FileWritten>();
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe(" \t");
    }

    [Fact]
    public async Task WriteAsync_WhenModeReplaceExistingAndFileMissing_DoesNotCreateTarget()
    {
        var fs = CreateFileSystem();

        var result = await fs.WriteAsync(new FileWriteRequest(
            new FileSystemPath("notes.txt"), "replacement", FileWriteMode.ReplaceExisting, TestSecurity.Grant()),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<FileWriteFailed>().SafeMessage.ShouldContain("does not exist");
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenFileExceedsMaximumReadBytes_ReturnsFileReadFailed()
    {
        var fs = CreateFileSystem(o => o.MaximumReadBytes = 4);
        fs.Seed(new FileSystemPath("notes.txt"), "this is too long");
        var result = await fs.ReadAsync(new FileReadRequest(new FileSystemPath("notes.txt"), TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileReadFailed>();
    }

    [Fact]
    public async Task WriteAsync_WhenContentExceedsMaximumWriteBytes_ReturnsFileWriteDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 4);
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "this is too long", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWriteDenied>();
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenGrantDenied_DoesNotMutateAndUsesExactDispositionBinding()
    {
        var store = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Revoked, 1, "Grant is revoked."),
        };
        var fs = CreateFileSystem(grantStore: store);
        fs.Seed(new FileSystemPath("notes.txt"), "original");
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<FileWriteDenied>().SafeMessage.ShouldBe("Grant is revoked.");
        fs.TryReadAllBytes(new FileSystemPath("notes.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("original");
        var enforcement = store.LastEnforcement.ShouldNotBeNull();
        enforcement.Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.WriteFingerprint(new FileSystemPath("notes.txt"), "replacement", FileWriteMode.CreateOrOverwrite));
    }

    [Fact]
    public void Seed_WhenParentMissing_ThrowsInvalidOperationException()
    {
        var fs = CreateFileSystem();
        _ = Should.Throw<InvalidOperationException>(() => fs.Seed(new FileSystemPath("sub/notes.txt"), "content"));
    }

    [Fact]
    public void Seed_WhenPathIsDirectory_ThrowsInvalidOperationException()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("sub"));
        _ = Should.Throw<InvalidOperationException>(() => fs.Seed(new FileSystemPath("sub"), "content"));
    }

    [Fact]
    public void CreateDirectory_WhenFileOccupiesAncestor_ThrowsInvalidOperationException()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("sub"), "content");
        _ = Should.Throw<InvalidOperationException>(() => fs.CreateDirectory(new FileSystemPath("sub/nested")));
    }

    [Fact]
    public async Task CreateDirectory_CreatesEveryMissingAncestor()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("src/nested"));
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(new FileSystemPath("src/nested"), 10, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(DirectoryEnumerationStatus.Success);
    }

    [Fact]
    public void TryReadAllBytes_WhenAbsent_ReturnsFalse()
    {
        var fs = CreateFileSystem();
        fs.TryReadAllBytes(new FileSystemPath("missing.txt"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task EnumerateAsync_WhenRootContainsEntries_ReturnsOrdinalPagesWithStableCursor()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("b.txt"), "b");
        fs.Seed(new FileSystemPath("a.txt"), "a");
        fs.CreateDirectory(new FileSystemPath("c"));
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
        fs.Seed(new FileSystemPath("a.txt"), "a");
        fs.Seed(new FileSystemPath("b.txt"), "b");
        var first = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        fs.Seed(new FileSystemPath("c.txt"), "c");
        var resumed = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, first.Continuation, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        resumed.Status.ShouldBe(DirectoryEnumerationStatus.SnapshotChanged);
        resumed.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnumerateAsync_WhenSnapshotExceedsConfiguredBound_ReturnsLimitExceeded()
    {
        var fs = CreateFileSystem(options => options.MaximumDirectorySnapshotEntries = 2);
        fs.Seed(new FileSystemPath("a.txt"), "a");
        fs.Seed(new FileSystemPath("b.txt"), "b");
        fs.Seed(new FileSystemPath("c.txt"), "c");
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 1, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(DirectoryEnumerationStatus.LimitExceeded);
    }

    [Fact]
    public async Task EnumerateAsync_WhenDirectoryDoesNotExist_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.EnumerateAsync(new DirectoryEnumerationRequest(new FileSystemPath("missing"), 10, null, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(DirectoryEnumerationStatus.NotFound);
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
            fs.CreateDirectory(new FileSystemPath("possibly-secret"));
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
        fs.CreateDirectory(new FileSystemPath("src/nested"));
        fs.Seed(new FileSystemPath("z.cs"), "z");
        fs.Seed(new FileSystemPath("src/b.cs"), "b");
        fs.Seed(new FileSystemPath("src/nested/a.cs"), "a");
        fs.Seed(new FileSystemPath("src/nested/note.txt"), "text");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Success);
        result.Complete.ShouldBeTrue();
        result.Matches.Select(static path => path.Value).ShouldBe(["src/b.cs", "src/nested/a.cs", "z.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenHiddenExcluded_DoesNotVisitDotPrefixedSubtrees()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath(".git"));
        fs.Seed(new FileSystemPath(".git/config"), "secret-ish");
        fs.Seed(new FileSystemPath("visible.txt"), "visible");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Success);
        result.Matches.Select(static path => path.Value).ShouldBe(["visible.txt"]);
        result.VisitedEntries.ShouldBe(1);
    }

    [Fact]
    public async Task GlobAsync_WhenSubtreeExcluded_PrunesItBeforeVisitBoundIsConsumed()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("bin/generated"));
        fs.CreateDirectory(new FileSystemPath("src"));
        fs.Seed(new FileSystemPath("bin/generated/one.cs"), "generated");
        fs.Seed(new FileSystemPath("bin/generated/two.cs"), "generated");
        fs.Seed(new FileSystemPath("src/target.cs"), "source");
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
        fs.Seed(new FileSystemPath("a.txt"), "a");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.NoMatches);
        result.Complete.ShouldBeTrue();
        result.Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task GlobAsync_WhenVisitBoundExceeded_ReturnsPartialIncompleteLimitOutcome()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.cs"), "a");
        fs.Seed(new FileSystemPath("b.cs"), "b");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("*.cs"), true, false, 1, 1, 10, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.VisitedEntries.ShouldBe(2);
        result.Matches.Select(static path => path.Value).ShouldBe(["a.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenBaseDirectoryMissing_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.GlobAsync(new GlobRequest(new FileSystemPath("missing"), new GlobPattern("*"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.NotFound);
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
    public async Task GlobAsync_WhenBaseDirectoryIsAFile_ReturnsDenied()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("not-a-directory.txt"), "content");
        var result = await fs.GlobAsync(new GlobRequest(new FileSystemPath("not-a-directory.txt"), new GlobPattern("*"), true, false, 10, 100, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.Denied);
    }

    [Fact]
    public async Task GlobAsync_WhenRetainedResultLimitReached_ReturnsLimitExceededDuringMatchRetention()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.cs"), "a");
        fs.Seed(new FileSystemPath("b.cs"), "b");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("*.cs"), true, false, 10, 100, 1, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.Matches.Select(static path => path.Value).ShouldBe(["a.cs"]);
    }

    [Fact]
    public async Task GlobAsync_WhenLimitIsReachedDeepInTraversal_PropagatesTerminalStatusToAncestor()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("sub"));
        fs.Seed(new FileSystemPath("other.cs"), "other");
        fs.Seed(new FileSystemPath("sub/deep.cs"), "deep");
        var result = await fs.GlobAsync(new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, 5, 2, 20, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GlobStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        result.VisitedEntries.ShouldBe(3);
        result.Matches.Select(static path => path.Value).ShouldBe(["other.cs"]);
    }

    [Fact]
    public async Task EnumerateAsync_WhenCallerCancelsDuringGrantConsumption_PropagatesCancellationWithoutObservingDirectory()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestSecurity.RecordingGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
        var fs = CreateFileSystem(grantStore: store);
        var action = async () => await fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 10, null, TestSecurity.Grant()), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnumerateAsync_WhenGrantStoreThrowsUnexpectedException_LogsFailureAndRethrows()
    {
        var fs = CreateFileSystem(grantStore: new ThrowingGrantStore());
        _ = await Should.ThrowAsync<InvalidOperationException>(() => fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 10, null, TestSecurity.Grant()), TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task WriteAsync_WhenLoggerIsEnabledAndWriteSucceeds_EmitsCompletedStructuredEvent()
    {
        var logger = new RecordingLogger<InMemoryFileSystem>();
        var fs = CreateFileSystem(logger: logger);
        var grant = TestSecurity.Grant();
        var result = await fs.WriteAsync(new FileWriteRequest(new FileSystemPath("notes.txt"), "hello", FileWriteMode.CreateOrOverwrite, grant), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<FileWritten>();
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(11100);
        completed.State["Operation"].ShouldBe("write");
        completed.State["SecurityRequestId"].ShouldBe(grant.RequestId);
        completed.State["Outcome"].ShouldBe("written");
        completed.Message.ShouldNotContain("notes.txt");
    }

    [Fact]
    public async Task EnumerateAsync_WhenLoggerIsEnabledAndGrantStoreThrowsUnexpectedException_EmitsFailedStructuredEvent()
    {
        var logger = new RecordingLogger<InMemoryFileSystem>();
        var fs = CreateFileSystem(grantStore: new ThrowingGrantStore(), logger: logger);
        var grant = TestSecurity.Grant();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => fs.EnumerateAsync(new DirectoryEnumerationRequest(null, 10, null, grant), TestContext.Current.CancellationToken).AsTask());
        var failed = logger.Snapshot().Single(static entry => entry.EventId.Id == 11101);
        failed.State["Operation"].ShouldBe("enumerate");
        failed.State["SecurityRequestId"].ShouldBe(grant.RequestId);
        failed.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    private sealed class ThrowingGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected grant store failure.");

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected grant store failure.");

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
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

    [Fact]
    public async Task SearchAsync_WhenLiteralMatches_ReturnsDeterministicVersionedByteLocations()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("src"));
        fs.Seed(new FileSystemPath("src/b.cs"), "first\nNeedle café\n");
        fs.Seed(new FileSystemPath("src/a.cs"), "needle\n");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), caseSensitive: false), TestContext.Current.CancellationToken);
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
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("src"));
        fs.Seed(new FileSystemPath("src/code.cs"), "item-42 item-x");
        fs.Seed(new FileSystemPath("src/notes.md"), "item-99");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern( /*lang=regex*/"item-[0-9]+", FileSearchPatternKind.RegularExpression), pathPattern: new GlobPattern("**/*.cs")), TestContext.Current.CancellationToken);
        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/code.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenSubtreeExcluded_PrunesItBeforeCandidateFileBoundIsConsumed()
    {
        var fs = CreateFileSystem();
        fs.CreateDirectory(new FileSystemPath("bin/generated"));
        fs.CreateDirectory(new FileSystemPath("src"));
        fs.Seed(new FileSystemPath("bin/generated/one.cs"), "needle");
        fs.Seed(new FileSystemPath("bin/generated/two.cs"), "needle");
        fs.Seed(new FileSystemPath("src/target.cs"), "needle");
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

        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        result.VisitedFiles.ShouldBe(1);
        result.Matches.ShouldHaveSingleItem().Path.Value.ShouldBe("src/target.cs");
    }

    [Fact]
    public async Task SearchAsync_WhenFilesAreHiddenBinaryOrInvalidUtf8_ExcludesThemWithoutDecoding()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath(".hidden"), "needle");
        fs.Seed(new FileSystemPath("binary"), "needle\0tail"u8.ToArray());
        fs.Seed(new FileSystemPath("invalid"), [0x6e, 0x65, 0x65, 0x64, 0x6c, 0x65, 0xff]);
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NoMatches);
        result.Matches.ShouldBeEmpty();
        result.VisitedFiles.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchLimitReached_ReturnsTypedPartialResult()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), "needle needle");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.Complete.ShouldBeFalse();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenRetainedMatchesExactlyEqualLimit_RemainsComplete()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), "one needle");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 1), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Complete.ShouldBeTrue();
        _ = result.Matches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenMatchingLineIsLong_RetainsBoundedContextContainingMatch()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), $"{new string('x', 200)}needle{new string('y', 200)}");
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 1024 * 1024, 100, 30, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        var match = result.Matches.ShouldHaveSingleItem();
        match.LineText.ShouldContain("needle");
        match.LineTextTruncated.ShouldBeTrue();
        match.LineProjectionByteOffset.ShouldBeGreaterThan(0);
        Encoding.UTF8.GetByteCount(match.LineText).ShouldBeLessThanOrEqualTo(30);
    }

    [Fact]
    public async Task SearchAsync_WhenProjectionWindowWouldSplitAMultiByteCharacter_AdjustsBothEdgesToValidUtf8()
    {
        // "é" is 2 UTF-8 bytes (0xC3 0xA9); with MaximumLineBytes=29 the naive half-context math lands the
        // leading edge on a continuation byte and the trailing edge mid-character, forcing both the
        // leading-edge continuation-byte skip and the trailing shrink-until-valid retry.
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), $"{new string('é', 100)}needle{new string('é', 100)}");
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 1024 * 1024, 100, 29, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        var match = result.Matches.ShouldHaveSingleItem();
        match.LineText.ShouldContain("needle");
        match.LineTextTruncated.ShouldBeTrue();
        Encoding.UTF8.GetByteCount(match.LineText).ShouldBeLessThanOrEqualTo(29);
    }

    [Fact]
    public async Task SearchAsync_WhenBaseDirectoryDoesNotExist_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var request = SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("missing"));
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.NotFound);
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_WhenBaseDirectoryIsAFile_ReturnsDenied()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("not-a-directory.txt"), "content");
        var request = SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("not-a-directory.txt"));
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Denied);
    }

    [Fact]
    public async Task SearchAsync_WhenRequestExceedsHostCeiling_DeniesBeforeTraversal()
    {
        var fs = CreateFileSystem(o => o.MaximumSearchMatches = 1);
        fs.Seed(new FileSystemPath("a.txt"), "needle");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumMatches: 2), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.Denied);
        result.VisitedFiles.ShouldBe(0);
        result.VisitedBytes.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_WhenGrantDenied_DoesNotCheckBaseExistence()
    {
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var fs = CreateFileSystem(grantStore: grantStore);
        var request = SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), basePath: new FileSystemPath("missing"));
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
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
        var fs = CreateFileSystem(timeProvider: new AdvancingTimeProvider());
        fs.Seed(new FileSystemPath("a.txt"), "needle");
        var result = await fs.SearchAsync(SearchRequest(new FileSearchPattern("needle", FileSearchPatternKind.Literal), maximumDuration: TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.TimedOut);
        result.Complete.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_WhenLaterSiblingFollowsATerminatedSubtree_SkipsItWithoutOpening()
    {
        var fs = CreateFileSystem();
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            fs.CreateDirectory(new FileSystemPath(name));
            fs.Seed(new FileSystemPath($"{name}/needle.txt"), "needle");
        }

        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 2, 1024 * 1024, 100, 1024, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.VisitedFiles.ShouldBe(2);
        result.Matches.Select(static match => match.Path.Value).ShouldBe(["a/needle.txt", "b/needle.txt"]);
    }

    [Fact]
    public async Task SearchAsync_WhenCandidateExceedsRemainingByteBudget_ReturnsLimitExceeded()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("big.txt"), new string('x', 100));
        var request = new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, 10, 100, 10, 100, 1024, TimeSpan.FromSeconds(10), TestSecurity.Grant());
        var result = await fs.SearchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSearchStatus.LimitExceeded);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("observed-byte");
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenSuccessful_ReturnsExactBytesHashAndEnforcement()
    {
        byte[] bytes = [0xef, 0xbb, 0xbf, 0x61, 0x0d, 0x0a];
        var grantStore = new TestSecurity.RecordingGrantStore();
        var fs = CreateFileSystem(grantStore: grantStore);
        fs.Seed(new FileSystemPath("a.txt"), bytes);
        var request = new FileSnapshotRequest(new FileSystemPath("a.txt"), 100, TestSecurity.Grant());
        var result = await fs.ReadSnapshotAsync(request, TestContext.Current.CancellationToken);
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
        var fs = CreateFileSystem(grantStore: grantStore);
        var result = await fs.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenRequestMaximumBytesExceedsConfiguredBound_ReturnsDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumReadBytes = 10);
        fs.Seed(new FileSystemPath("a.txt"), "content");
        var result = await fs.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("a.txt"), 1000, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.Denied);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("host boundary");
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenTargetIsMissing_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("missing.txt"), 100, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.NotFound);
    }

    [Fact]
    public async Task ReadSnapshotAsync_WhenFileExceedsRequestByteBound_ReturnsLimitExceeded()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("big.txt"), "0123456789");
        var result = await fs.ReadSnapshotAsync(new FileSnapshotRequest(new FileSystemPath("big.txt"), 4, TestSecurity.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FileSnapshotStatus.LimitExceeded);
    }

    [Fact]
    public async Task ReplaceAsync_WhenContentExceedsMaximumWriteBytes_ReturnsDenied()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 2);
        fs.Seed(new FileSystemPath("a.txt"), "current");
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var bytes).ShouldBeTrue();
        var expected = FileSecurityBinding.ContentFingerprint(bytes.AsSpan());
        var result = await fs.ReplaceAsync(ReplaceRequest("a.txt", expected, "too long"u8.ToArray().ToImmutableArray(), MutationId(6)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var stillCurrent).ShouldBeTrue();
        Encoding.UTF8.GetString(stillCurrent.AsSpan()).ShouldBe("current");
    }

    [Fact]
    public async Task ReplaceAsync_WhenTargetIsMissing_ReturnsNotFound()
    {
        var fs = CreateFileSystem();
        var result = await fs.ReplaceAsync(ReplaceRequest("missing.txt", new ContentHash("sha256:any"), "content"u8.ToArray().ToImmutableArray(), MutationId(7)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.NotFound);
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionMatches_CommitsAtomically()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("script.sh"), "old\n");
        fs.TryReadAllBytes(new FileSystemPath("script.sh"), out var original).ShouldBeTrue();
        var replacement = "new\r\n"u8.ToArray().ToImmutableArray();
        var grantStore = new TestSecurity.RecordingGrantStore();
        var replacingFs = CreateFileSystem(grantStore: grantStore);
        replacingFs.Seed(new FileSystemPath("script.sh"), "old\n");
        var request = ReplaceRequest("script.sh", FileSecurityBinding.ContentFingerprint(original.AsSpan()), replacement, MutationId(1));
        var result = await replacingFs.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Committed);
        result.ContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(replacement.AsSpan()));
        replacingFs.TryReadAllBytes(new FileSystemPath("script.sh"), out var committed).ShouldBeTrue();
        committed.ShouldBe(replacement);
        var enforcement = grantStore.LastEnforcement.ShouldNotBeNull();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Replace);
        enforcement.Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path));
        enforcement.InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
    }

    [Fact]
    public async Task ReplaceAsync_WhenExpectedVersionChanged_ReturnsConflictWithoutMutation()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), "current");
        var request = ReplaceRequest("a.txt", new ContentHash("sha256:stale"), "replacement"u8.ToArray().ToImmutableArray(), MutationId(2));
        var result = await fs.ReplaceAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Conflict);
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("current");
    }

    [Fact]
    public async Task ReplaceAsync_WhenGrantDenied_DoesNotMutate()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), "current");
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var bytes).ShouldBeTrue();
        var grantStore = new TestSecurity.RecordingGrantStore
        {
            Result = new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Denied."),
        };
        var deniedFs = CreateFileSystem(grantStore: grantStore);
        deniedFs.Seed(new FileSystemPath("a.txt"), "current");
        var result = await deniedFs.ReplaceAsync(ReplaceRequest("a.txt", FileSecurityBinding.ContentFingerprint(bytes.AsSpan()), "replacement"u8.ToArray().ToImmutableArray(), MutationId(3)), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AtomicFileReplaceStatus.Denied);
        deniedFs.TryReadAllBytes(new FileSystemPath("a.txt"), out var stillCurrent).ShouldBeTrue();
        Encoding.UTF8.GetString(stillCurrent.AsSpan()).ShouldBe("current");
    }

    [Fact]
    public async Task ReplaceAsync_WhenTwoPlansRace_OnlyOneExpectedVersionCommits()
    {
        var fs = CreateFileSystem();
        fs.Seed(new FileSystemPath("a.txt"), "old");
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var bytes).ShouldBeTrue();
        var expected = FileSecurityBinding.ContentFingerprint(bytes.AsSpan());
        var first = fs.ReplaceAsync(ReplaceRequest("a.txt", expected, "first"u8.ToArray().ToImmutableArray(), MutationId(4)), TestContext.Current.CancellationToken).AsTask();
        var second = fs.ReplaceAsync(ReplaceRequest("a.txt", expected, "second"u8.ToArray().ToImmutableArray(), MutationId(5)), TestContext.Current.CancellationToken).AsTask();
        var results = await Task.WhenAll(first, second);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Committed).ShouldBe(1);
        results.Count(static result => result.Status == AtomicFileReplaceStatus.Conflict).ShouldBe(1);
        fs.TryReadAllBytes(new FileSystemPath("a.txt"), out var final).ShouldBeTrue();
        var content = Encoding.UTF8.GetString(final.AsSpan());
        (content is "first" or "second").ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateIsValid_CommitsAtomicallyWithExactSecurityBinding()
    {
        var store = new RecordingGrantStore();
        var fs = CreateFileSystem(grantStore: store);
        var entry = new WorkspacePatchCreate(PatchMutationId(1), new FileSystemPath("created.txt"), "hello\r\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.AtomicCommitted);
        result.Entries.Single().Status.ShouldBe(WorkspacePatchEntryStatus.Committed);
        fs.TryReadAllBytes(new FileSystemPath("created.txt"), out var bytes).ShouldBeTrue();
        bytes.ShouldBe(entry.Content);
        var enforcement = store.Enforcements.Single();
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe(WorkspacePatchSecurityBinding.CreateResources(entry.Id, entry.Path));
        enforcement.InputFingerprint.ShouldBe(WorkspacePatchSecurityBinding.CreateFingerprint(entry.Id, entry.Path, entry.Content));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenMixedPlanIsValid_CommitsInSourceOrderWithHonestVisibilityStatus()
    {
        var store = new RecordingGrantStore();
        var fs = CreateFileSystem(grantStore: store);
        fs.Seed(new FileSystemPath("replace.sh"), "old");
        fs.Seed(new FileSystemPath("delete.txt"), "delete");
        fs.Seed(new FileSystemPath("move.txt"), "move");
        var replaceExpected = Fingerprint(fs, "replace.sh");
        var deleteExpected = Fingerprint(fs, "delete.txt");
        var moveExpected = Fingerprint(fs, "move.txt");
        var create = new WorkspacePatchCreate(PatchMutationId(2), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var replace = new WorkspacePatchReplace(PatchMutationId(3), new FileSystemPath("replace.sh"), replaceExpected, "updated\n"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var delete = new WorkspacePatchDelete(PatchMutationId(4), new FileSystemPath("delete.txt"), deleteExpected, TestSecurity.Grant());
        var move = new WorkspacePatchMove(PatchMutationId(5), new FileSystemPath("move.txt"), new FileSystemPath("moved.txt"), moveExpected, TestSecurity.Grant());
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([create, replace, delete, move]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.CommittedWithNonAtomicVisibility);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Committed);
        fs.TryReadAllBytes(new FileSystemPath("new.txt"), out var newBytes).ShouldBeTrue();
        Encoding.UTF8.GetString(newBytes.AsSpan()).ShouldBe("new");
        fs.TryReadAllBytes(new FileSystemPath("replace.sh"), out var replacedBytes).ShouldBeTrue();
        Encoding.UTF8.GetString(replacedBytes.AsSpan()).ShouldBe("updated\n");
        fs.TryReadAllBytes(new FileSystemPath("delete.txt"), out _).ShouldBeFalse();
        fs.TryReadAllBytes(new FileSystemPath("move.txt"), out _).ShouldBeFalse();
        fs.TryReadAllBytes(new FileSystemPath("moved.txt"), out var movedBytes).ShouldBeTrue();
        Encoding.UTF8.GetString(movedBytes.AsSpan()).ShouldBe("move");
        store.Enforcements.Select(static item => item.Effect).ShouldBe([SecurityEffect.Create, SecurityEffect.Replace, SecurityEffect.Delete, SecurityEffect.Move,]);
        store.Enforcements[1].Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(replace.Id, replace.Path));
        store.Enforcements[1].InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(replace.Id, replace.Path, replace.ExpectedContentFingerprint, replace.Content));
        store.Enforcements[2].Resources.ShouldBe(WorkspacePatchSecurityBinding.DeleteResources(delete.Path));
        store.Enforcements[3].Resources.ShouldBe(WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath));
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterPreconditionIsStale_RejectsWholePlanBeforeEffects()
    {
        var fs = CreateFileSystem(grantStore: new RecordingGrantStore());
        fs.Seed(new FileSystemPath("existing.txt"), "current");
        var create = new WorkspacePatchCreate(PatchMutationId(6), new FileSystemPath("new.txt"), "new"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var stale = new WorkspacePatchReplace(PatchMutationId(7), new FileSystemPath("existing.txt"), new ContentHash("sha256:stale"), "changed"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([create, stale]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.Entries.Select(static item => item.Status).ShouldAllBe(static status => status == WorkspacePatchEntryStatus.Unchanged);
        fs.TryReadAllBytes(new FileSystemPath("new.txt"), out _).ShouldBeFalse();
        fs.TryReadAllBytes(new FileSystemPath("existing.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("current");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenLaterGrantIsDenied_ObservesAndMutatesNothing()
    {
        var store = new RecordingGrantStore(deniedIndex: 1);
        var fs = CreateFileSystem(grantStore: store);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(PatchMutationId(8), new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchDelete(PatchMutationId(9), new FileSystemPath("secret-missing.txt"), new ContentHash("sha256:any"), TestSecurity.Grant()));
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.Count.ShouldBe(2);
        fs.TryReadAllBytes(new FileSystemPath("first.txt"), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenPathsOverlap_RejectsBeforeGrantConsumption()
    {
        var store = new RecordingGrantStore();
        var fs = CreateFileSystem(grantStore: store);
        var path = new FileSystemPath("same.txt");
        var request = new WorkspacePatchRequest([
            new WorkspacePatchCreate(PatchMutationId(10), path, "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchDelete(PatchMutationId(11), path, new ContentHash("sha256:any"), TestSecurity.Grant()),
        ]);
        var result = await fs.ApplyPatchAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        store.Enforcements.ShouldBeEmpty();
        fs.TryReadAllBytes(path, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenCreateTargetExists_DoesNotReplaceIt()
    {
        var fs = CreateFileSystem(grantStore: new RecordingGrantStore());
        fs.Seed(new FileSystemPath("existing.txt"), "original");
        var entry = new WorkspacePatchCreate(PatchMutationId(12), new FileSystemPath("existing.txt"), "replacement"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        fs.TryReadAllBytes(new FileSystemPath("existing.txt"), out var bytes).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes.AsSpan()).ShouldBe("original");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenEntryCountExceedsConfiguredBoundary_RejectsBeforeGrantConsumption()
    {
        var fs = CreateFileSystem(o => o.MaximumPatchEntries = 1, grantStore: new RecordingGrantStore());
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(PatchMutationId(20), new FileSystemPath("one.txt"), "one"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(PatchMutationId(21), new FileSystemPath("two.txt"), "two"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("entry boundary");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenEntryKindDoesNotMatchItsConcreteContract_RejectsBeforeGrantConsumption()
    {
        var fs = CreateFileSystem(grantStore: new RecordingGrantStore());
        var mismatched = new WorkspacePatchCreate(PatchMutationId(22), new FileSystemPath("mismatched.txt"), "content"u8.ToArray().ToImmutableArray(), TestSecurity.Grant())
            with
        { Kind = WorkspacePatchEntryKind.Delete };
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([mismatched]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("does not match its concrete contract");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenTwoEntriesShareTheSameMutationIdentity_RejectsBeforeGrantConsumption()
    {
        var fs = CreateFileSystem(grantStore: new RecordingGrantStore());
        var sharedId = PatchMutationId(23);
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(sharedId, new FileSystemPath("first.txt"), "first"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(sharedId, new FileSystemPath("second.txt"), "second"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("unique");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenSingleEntryContentExceedsWriteBoundary_RejectsBeforeGrantConsumption()
    {
        var fs = CreateFileSystem(o => o.MaximumWriteBytes = 2, grantStore: new RecordingGrantStore());
        var entry = new WorkspacePatchCreate(PatchMutationId(24), new FileSystemPath("too-big.txt"), "too long"u8.ToArray().ToImmutableArray(), TestSecurity.Grant());
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest([entry]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("write boundary");
    }

    [Fact]
    public async Task ApplyPatchAsync_WhenAggregateContentExceedsPatchByteBoundary_RejectsBeforeGrantConsumption()
    {
        var fs = CreateFileSystem(o => o.MaximumPatchBytes = 5, grantStore: new RecordingGrantStore());
        var entries = ImmutableArray.Create<WorkspacePatchEntry>(
            new WorkspacePatchCreate(PatchMutationId(25), new FileSystemPath("one.txt"), "abc"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()),
            new WorkspacePatchCreate(PatchMutationId(26), new FileSystemPath("two.txt"), "abc"u8.ToArray().ToImmutableArray(), TestSecurity.Grant()));
        var result = await fs.ApplyPatchAsync(new WorkspacePatchRequest(entries), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(WorkspacePatchStatus.RejectedBeforeEffect);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("aggregate byte boundary");
    }

    private static InMemoryFileSystem CreateFileSystem(
        Action<InMemoryFileSystemOptions>? configure = null,
        ISecurityGrantStore? grantStore = null,
        TimeProvider? timeProvider = null,
        ILogger<InMemoryFileSystem>? logger = null)
    {
        var options = new InMemoryFileSystemOptions();
        configure?.Invoke(options);
        return new InMemoryFileSystem(Options.Create(options), grantStore ?? TestSecurity.GrantStore(), timeProvider ?? TimeProvider.System, logger);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;

    private static FileSearchRequest SearchRequest(
        FileSearchPattern pattern,
        FileSystemPath? basePath = null,
        GlobPattern? pathPattern = null,
        bool caseSensitive = true,
        int maximumMatches = 100,
        TimeSpan? maximumDuration = null) => new(
        basePath, pattern, pathPattern ?? new GlobPattern("**/*"), caseSensitive, includeHidden: false,
        maximumDepth: 10, maximumFiles: 100, maximumBytes: 1024 * 1024, maximumMatches, maximumLineBytes: 1024,
        maximumDuration ?? TimeSpan.FromSeconds(10), TestSecurity.Grant());

    private sealed class AdvancingTimeProvider: TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1;

        public override long GetTimestamp() => Interlocked.Increment(ref _timestamp);
    }

    private static AtomicFileReplaceRequest ReplaceRequest(string path, ContentHash expected, ImmutableArray<byte> content, WorkspaceMutationId id) =>
        new(id, new FileSystemPath(path), expected, content, TestSecurity.Grant());

    private static WorkspaceMutationId MutationId(int suffix) => new(Guid.Parse($"10000000-0000-0000-0000-{suffix:D12}"));

    private static WorkspaceMutationId PatchMutationId(int suffix) => new(Guid.Parse($"20000000-0000-0000-0000-{suffix:D12}"));

    private static ContentHash Fingerprint(InMemoryFileSystem fs, string path)
    {
        fs.TryReadAllBytes(new FileSystemPath(path), out var bytes).ShouldBeTrue();
        return FileSecurityBinding.ContentFingerprint(bytes.AsSpan());
    }

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
}
