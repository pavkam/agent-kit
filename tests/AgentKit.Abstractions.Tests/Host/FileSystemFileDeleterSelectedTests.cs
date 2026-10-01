// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileSystemFileDeleterSelected argument validation and retained evidence.</summary>
public sealed class FileSystemFileDeleterSelectedTests
{
    private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Delete);

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        var deleter = new RecordingDeleter();

        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemFileDeleterSelected(default, deleter, _capabilities)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new FileSystemFileDeleterSelected(new FileSystemProfileKey("p"), null!, _capabilities)).ParamName.ShouldBe("deleter");
        Should.Throw<ArgumentNullException>(() => new FileSystemFileDeleterSelected(new FileSystemProfileKey("p"), deleter, null!)).ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void Constructor_WhenValid_RetainsKeyDeleterAndCapabilities()
    {
        var deleter = new RecordingDeleter();

        var selected = new FileSystemFileDeleterSelected(new FileSystemProfileKey("p"), deleter, _capabilities);

        selected.Key.ShouldBe(new FileSystemProfileKey("p"));
        selected.Deleter.ShouldBeSameAs(deleter);
        selected.Capabilities.ShouldBe(_capabilities);
    }

    private sealed class RecordingDeleter: IFileDeleter
    {
        public ComponentId SecurityAudience { get; } = new("test.deleter");

        public ValueTask<FileDeleteResult> DeleteAsync(AuthorizedFileDelete operation, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<FileDeleteResult>(new FileDeleteUnsupported());
    }
}
