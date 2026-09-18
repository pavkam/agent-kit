// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that the advisory store lock is genuinely exclusive on one host and that release is safe and repeatable.</summary>
/// <remarks>
/// The lock exists so a second writer fails fast rather than interleaving appends into one root. Contention is therefore
/// asserted by actually holding the lock while attempting a second acquisition, not by inspecting any flag the adapter keeps.
/// </remarks>
public sealed class JsonStoreLockTests
{
    /// <summary>Verifies a null path is rejected before any file handle is opened.</summary>
    [Fact]
    public void Acquire_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonStoreLock.Acquire(null!));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a whitespace-only path is rejected as blank rather than treated as a relative lock file.</summary>
    [Fact]
    public void Acquire_WhenPathIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => JsonStoreLock.Acquire("  "));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies acquisition creates the lock file and keeps it present for the holder's lifetime.</summary>
    [Fact]
    public void Acquire_WhenRootIsFree_CreatesAndHoldsLockFile()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("store.lock");

        using var storeLock = JsonStoreLock.Acquire(path);

        File.Exists(path).ShouldBeTrue();
    }

    /// <summary>Verifies a second acquisition of a held root fails closed instead of allowing interleaved writers.</summary>
    [Fact]
    public void Acquire_WhenRootIsAlreadyHeld_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("store.lock");
        using var held = JsonStoreLock.Acquire(path);

        _ = Should.Throw<InvalidOperationException>(() => JsonStoreLock.Acquire(path));
    }

    /// <summary>Verifies a missing parent directory surfaces as an I/O failure rather than as false contention.</summary>
    [Fact]
    public void Acquire_WhenDirectoryIsMissing_ThrowsIOException()
    {
        using var directory = new TestTemporaryDirectory();

        _ = Should.Throw<IOException>(() => JsonStoreLock.Acquire(Path.Combine(directory.Path, "absent", "store.lock")));
    }

    /// <summary>Verifies releasing the lock removes its file and makes the root immediately acquirable again.</summary>
    [Fact]
    public void Dispose_WhenCalled_ReleasesRootForReacquisition()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("store.lock");
        var first = JsonStoreLock.Acquire(path);

        first.Dispose();

        File.Exists(path).ShouldBeFalse();
        using var second = JsonStoreLock.Acquire(path);
        File.Exists(path).ShouldBeTrue();
    }

    /// <summary>Verifies repeated release is a no-op, so an owner may dispose defensively without risking a second close.</summary>
    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();
        var storeLock = JsonStoreLock.Acquire(directory.Combine("store.lock"));

        storeLock.Dispose();

        Should.NotThrow(storeLock.Dispose);
    }

    /// <summary>Verifies a lock path that resolves through a symbolic link is refused rather than silently redirected.</summary>
    [Fact]
    public void Acquire_WhenLockPathIsSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new TestTemporaryDirectory();
        var target = directory.Combine("real.lock");
        File.WriteAllBytes(target, []);
        var link = directory.Combine("store.lock");
        _ = File.CreateSymbolicLink(link, target);

        _ = Should.Throw<InvalidOperationException>(() => JsonStoreLock.Acquire(link));
    }
}
