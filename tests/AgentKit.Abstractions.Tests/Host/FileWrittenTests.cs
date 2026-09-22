// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies LegacyFileWritten behavior and contracts.</summary>
public sealed class FileWrittenTests
{
    [Fact]
    [Obsolete("Legacy host surface.")]

    public void FileWritten_Constructor_WhenBytesWrittenNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LegacyFileWritten(-1));
        exception.ParamName.ShouldBe("bytesWritten");
    }

    [Fact]
    [Obsolete("Legacy host surface.")]

    public void FileWritten_Constructor_WhenValid_RoundTripsBytesWritten()
    {
        var written = new LegacyFileWritten(42);
        written.BytesWritten.ShouldBe(42);
    }

    [Fact]
    [Obsolete("Legacy host surface.")]

    public void FileWritten_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LegacyFileWritten(42);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
