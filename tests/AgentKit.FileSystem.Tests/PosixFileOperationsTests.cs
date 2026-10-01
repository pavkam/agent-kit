// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

/// <summary>Verifies descriptor-relative traversal and root-confinement helpers shared by the operating-system adapters.</summary>
public sealed class PosixFileOperationsTests: IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agentkit-posix-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates an isolated root directory.</summary>
    public PosixFileOperationsTests() => _ = Directory.CreateDirectory(_root);

    /// <summary>Verifies a host path equal to root plus relative path is accepted.</summary>
    [Fact]
    public void IsResolvedUnderRoot_WhenHostPathMatchesRelativePath_ReturnsTrue()
    {
        var target = Target("src/a.txt", Path.Combine(_root, "src", "a.txt"));

        PosixFileOperations.IsResolvedUnderRoot(target, _root).ShouldBeTrue();
    }

    /// <summary>Verifies a host path that names a different location than the bound relative path is refused.</summary>
    [Fact]
    public void IsResolvedUnderRoot_WhenHostPathDiffersFromRelativePath_ReturnsFalse()
    {
        var target = Target("src/a.txt", Path.Combine(_root, "other", "a.txt"));

        PosixFileOperations.IsResolvedUnderRoot(target, _root).ShouldBeFalse();
    }

    /// <summary>Verifies a host path outside the root is refused.</summary>
    [Fact]
    public void IsResolvedUnderRoot_WhenHostPathEscapesTheRoot_ReturnsFalse()
    {
        var target = Target("a.txt", Path.Combine(Path.GetDirectoryName(_root)!, "a.txt"));

        PosixFileOperations.IsResolvedUnderRoot(target, _root).ShouldBeFalse();
    }

    /// <summary>Verifies a null target is rejected with its parameter name.</summary>
    [Fact]
    public void IsResolvedUnderRoot_WhenTargetIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => PosixFileOperations.IsResolvedUnderRoot(null!, _root)).ParamName.ShouldBe("target");

    /// <summary>Verifies a blank root is rejected with its parameter name.</summary>
    [Fact]
    public void IsResolvedUnderRoot_WhenRootIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => PosixFileOperations.IsResolvedUnderRoot(Target("a.txt", "/x/a.txt"), " ")).ParamName.ShouldBe("hostRootPath");

    /// <summary>Verifies traversal opens the parent of a nested path and reports the final segment.</summary>
    [Fact]
    public void TryOpenParentDirectory_WhenPathIsNested_ReturnsParentAndFileName()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.Combine(_root, "a", "b"));

        var opened = PosixFileOperations.TryOpenParentDirectory(_root, "a/b/file.txt", TestContext.Current.CancellationToken, out var parent, out var fileName, out var error);

        using (parent)
        {
            opened.ShouldBeTrue();
            fileName.ShouldBe("file.txt");
            error.ShouldBe(0);
        }
    }

    /// <summary>Verifies traversal refuses an intermediate symbolic link instead of following it.</summary>
    [Fact]
    public void TryOpenParentDirectory_WhenAnIntermediateSegmentIsASymbolicLink_RefusesAsABoundaryViolation()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), "agentkit-posix-outside-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_root, "link"), outside);

            var opened = PosixFileOperations.TryOpenParentDirectory(_root, "link/file.txt", TestContext.Current.CancellationToken, out _, out _, out var error);

            opened.ShouldBeFalse();
            PosixFileOperations.IsBoundaryViolation(error).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Verifies a missing intermediate directory is a not-found error rather than a boundary violation.</summary>
    [Fact]
    public void TryOpenParentDirectory_WhenAnIntermediateSegmentIsMissing_ReportsNotFound()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        var opened = PosixFileOperations.TryOpenParentDirectory(_root, "missing/file.txt", TestContext.Current.CancellationToken, out _, out _, out var error);

        opened.ShouldBeFalse();
        PosixFileOperations.IsBoundaryViolation(error).ShouldBeFalse();
    }

    /// <summary>Verifies a path with no file segment is refused before any descriptor is opened.</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("a/.")]
    public void TryOpenParentDirectory_WhenPathNamesNoFile_ReturnsFalse(string relativePath)
    {
        var opened = PosixFileOperations.TryOpenParentDirectory(_root, relativePath, TestContext.Current.CancellationToken, out _, out var fileName, out _);

        opened.ShouldBeFalse();
        fileName.ShouldBeEmpty();
    }

    /// <summary>Verifies the regular-file open wraps the validated descriptor and reports its length.</summary>
    [Fact]
    public void TryOpenRegularFileReadOnly_WhenFileExists_ReturnsSeekableStreamWithLength()
    {
        if (!PosixFileOperations.IsSecureTraversalSupported)
        {
            return;
        }

        File.WriteAllText(Path.Combine(_root, "f.txt"), "hello");

        var result = PosixFileOperations.TryOpenRegularFileReadOnly(_root, "f.txt", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(PosixFileOperations.PosixOpenReadStatus.Success);
        using var stream = result.Stream.ShouldNotBeNull();
        result.Length.ShouldBe(5);
        stream.CanSeek.ShouldBeTrue();
    }

    /// <summary>Verifies a null path is rejected before any effect.</summary>
    [Fact]
    public void TryOpenRegularFileReadOnly_WhenRelativePathIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => PosixFileOperations.TryOpenRegularFileReadOnly(_root, " ", TestContext.Current.CancellationToken))
            .ParamName.ShouldBe("relativePath");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ResolvedFileTarget Target(string relativePath, string hostPath) => new(
        new FileRootId("workspace"),
        new NormalizedRelativePath(relativePath),
        hostPath,
        FilePathComparisonKind.Ordinal,
        FileSecurityBinding.ContentFingerprint("no-link"u8),
        FileSecurityBinding.ContentFingerprint("target"u8));
}
