// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies store-root path resolution, log-name confinement, and link detection for directories and files.</summary>
/// <remarks>
/// Link detection is asserted against real symbolic links created on disk, because a fake cannot reproduce the
/// <see cref="FileSystemInfo.LinkTarget"/> and reparse-point attributes the check actually reads. The non-link cases use a
/// canonicalized temporary root so macOS's <c>/var</c> link does not make them pass or fail for the wrong reason.
/// </remarks>
public sealed class JsonStoreRootTests
{
    /// <summary>Verifies a null root directory is rejected before any path is composed.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new JsonStoreRoot(null!));
        exception.ParamName.ShouldBe("directoryPath");
    }

    /// <summary>Verifies a whitespace-only root directory is rejected as blank rather than resolved against the process directory.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonStoreRoot(" "));
        exception.ParamName.ShouldBe("directoryPath");
    }

    /// <summary>Verifies a relative root is normalized to a fully qualified path so later comparisons are stable.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsRelative_NormalizesToFullPath()
    {
        var root = new JsonStoreRoot("relative-store-root");

        root.DirectoryPath.ShouldBe(Path.GetFullPath("relative-store-root"));
    }

    /// <summary>Verifies an already absolute root is retained unchanged as the normalized directory.</summary>
    [Fact]
    public void DirectoryPath_WhenDirectoryPathIsAbsolute_ReturnsSamePath()
    {
        using var directory = new TestTemporaryDirectory();

        var root = new JsonStoreRoot(directory.Path);

        root.DirectoryPath.ShouldBe(directory.Path);
    }

    /// <summary>Verifies the manifest document is the fixed <c>store.json</c> inside the root.</summary>
    [Fact]
    public void ManifestPath_WhenConstructed_ResolvesStoreJsonInsideRoot()
    {
        using var directory = new TestTemporaryDirectory();

        var root = new JsonStoreRoot(directory.Path);

        root.ManifestPath.ShouldBe(Path.Combine(directory.Path, "store.json"));
    }

    /// <summary>Verifies the advisory lock is the fixed <c>store.lock</c> inside the root.</summary>
    [Fact]
    public void LockPath_WhenConstructed_ResolvesStoreLockInsideRoot()
    {
        using var directory = new TestTemporaryDirectory();

        var root = new JsonStoreRoot(directory.Path);

        root.LockPath.ShouldBe(Path.Combine(directory.Path, "store.lock"));
    }

    /// <summary>Verifies a simple log name becomes a newline-delimited log file inside the root.</summary>
    [Fact]
    public void LogPath_WhenNameIsSimple_AppendsJsonlExtensionInsideRoot()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        var path = root.LogPath("grants");

        path.ShouldBe(Path.Combine(directory.Path, "grants.jsonl"));
    }

    /// <summary>Verifies a null log name is rejected before any path is composed.</summary>
    [Fact]
    public void LogPath_WhenNameIsNull_ThrowsArgumentNullException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        var exception = Should.Throw<ArgumentNullException>(() => root.LogPath(null!));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a whitespace-only log name is rejected as blank rather than producing a hidden extension-only file.</summary>
    [Fact]
    public void LogPath_WhenNameIsBlank_ThrowsArgumentException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        var exception = Should.Throw<ArgumentException>(() => root.LogPath("\t"));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a log name containing the primary path separator cannot escape or nest inside the root.</summary>
    [Fact]
    public void LogPath_WhenNameContainsDirectorySeparator_ThrowsArgumentException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);
        var name = string.Concat("nested", Path.DirectorySeparatorChar.ToString(), "grants");

        var exception = Should.Throw<ArgumentException>(() => root.LogPath(name));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a log name containing the alternate path separator is refused on every host convention.</summary>
    [Fact]
    public void LogPath_WhenNameContainsAlternateDirectorySeparator_ThrowsArgumentException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);
        var name = string.Concat("nested", Path.AltDirectorySeparatorChar.ToString(), "grants");

        var exception = Should.Throw<ArgumentException>(() => root.LogPath(name));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a bare parent-traversal name is refused, so a log can never resolve outside the root.</summary>
    [Fact]
    public void LogPath_WhenNameIsParentTraversal_ThrowsArgumentException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        var exception = Should.Throw<ArgumentException>(() => root.LogPath(".."));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies an embedded parent-traversal sequence is refused even when it is surrounded by ordinary characters.</summary>
    [Fact]
    public void LogPath_WhenNameContainsEmbeddedParentTraversal_ThrowsArgumentException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        var exception = Should.Throw<ArgumentException>(() => root.LogPath("gr..ants"));

        exception.ParamName.ShouldBe("name");
    }

    /// <summary>Verifies a missing root fails closed when creation was not authorized by the caller.</summary>
    [Fact]
    public void Validate_WhenRootIsMissingAndCreateIsNotAllowed_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(Path.Combine(directory.Path, "absent"));

        _ = Should.Throw<InvalidOperationException>(() => root.Validate(allowCreate: false));

        Directory.Exists(Path.Combine(directory.Path, "absent")).ShouldBeFalse();
    }

    /// <summary>Verifies a missing root is created as a declared bootstrap effect when creation is authorized.</summary>
    [Fact]
    public void Validate_WhenRootIsMissingAndCreateIsAllowed_CreatesRoot()
    {
        using var directory = new TestTemporaryDirectory();
        var target = Path.Combine(directory.Path, "created", "nested");
        var root = new JsonStoreRoot(target);

        root.Validate(allowCreate: true);

        Directory.Exists(target).ShouldBeTrue();
    }

    /// <summary>Verifies an existing real root validates successfully and is not recreated or modified.</summary>
    [Fact]
    public void Validate_WhenRootExists_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();
        var root = new JsonStoreRoot(directory.Path);

        Should.NotThrow(() => root.Validate(allowCreate: false));
    }

    /// <summary>Verifies a root that is itself a symbolic link is refused, because the target can be replaced later.</summary>
    [Fact]
    public void Validate_WhenRootIsSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var target = directory.Combine("real");
        _ = Directory.CreateDirectory(target);
        var link = directory.Combine("link");
        _ = Directory.CreateSymbolicLink(link, target);
        var root = new JsonStoreRoot(link);

        _ = Should.Throw<InvalidOperationException>(() => root.Validate(allowCreate: false));
    }

    /// <summary>Verifies a root reached through a linked ancestor is refused, since the whole path must be stable.</summary>
    [Fact]
    public void Validate_WhenAncestorIsSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var target = directory.Combine("real");
        _ = Directory.CreateDirectory(Path.Combine(target, "store"));
        var link = directory.Combine("link");
        _ = Directory.CreateSymbolicLink(link, target);
        var root = new JsonStoreRoot(Path.Combine(link, "store"));

        _ = Should.Throw<InvalidOperationException>(() => root.Validate(allowCreate: false));
    }

    /// <summary>Verifies a null file path is rejected before the filesystem is inspected.</summary>
    [Fact]
    public void ValidateFile_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonStoreRoot.ValidateFile(null!));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a whitespace-only file path is rejected as blank rather than inspected.</summary>
    [Fact]
    public void ValidateFile_WhenPathIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => JsonStoreRoot.ValidateFile("   "));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a not-yet-created store file is accepted, because initialization must be able to create it.</summary>
    [Fact]
    public void ValidateFile_WhenFileIsMissing_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();

        Should.NotThrow(() => JsonStoreRoot.ValidateFile(directory.Combine("store.json")));
    }

    /// <summary>Verifies an ordinary regular file is accepted unchanged.</summary>
    [Fact]
    public void ValidateFile_WhenFileIsRegular_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("store.json");
        File.WriteAllBytes(path, "{}"u8.ToArray());

        Should.NotThrow(() => JsonStoreRoot.ValidateFile(path));
    }

    /// <summary>Verifies a store file that is a symbolic link is refused, because its target can be swapped out.</summary>
    [Fact]
    public void ValidateFile_WhenFileIsSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var target = directory.Combine("real.json");
        File.WriteAllBytes(target, "{}"u8.ToArray());
        var link = directory.Combine("store.json");
        _ = File.CreateSymbolicLink(link, target);

        _ = Should.Throw<InvalidOperationException>(() => JsonStoreRoot.ValidateFile(link));
    }

    /// <summary>Verifies a dangling symbolic link is refused rather than treated as a missing file that may be created.</summary>
    [Fact]
    public void ValidateFile_WhenFileIsDanglingSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var link = directory.Combine("store.json");
        _ = File.CreateSymbolicLink(link, directory.Combine("absent.json"));

        _ = Should.Throw<InvalidOperationException>(() => JsonStoreRoot.ValidateFile(link));
    }
}
