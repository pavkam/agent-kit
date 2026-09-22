// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileSystemCapabilities behavior and contracts.</summary>
public sealed class FileSystemCapabilitiesTests
{
    [Fact]
    public void Constructor_WhenSupportedUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new FileSystemCapabilities((FileSystemCapability) 999));
        exception.ParamName.ShouldBe("supported");
    }

    [Fact]
    public void Constructor_WhenSupportedRead_DoesNotThrow()
    {
        var capabilities = new FileSystemCapabilities(FileSystemCapability.Read);
        capabilities.Supported.ShouldBe(FileSystemCapability.Read);
    }
}
