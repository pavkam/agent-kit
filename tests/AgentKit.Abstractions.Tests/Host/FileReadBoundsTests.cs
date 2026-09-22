// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileReadBounds behavior and contracts.</summary>
public sealed class FileReadBoundsTests
{
    [Fact]
    public void Constructor_WhenMaxBytesNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileReadBounds(-1));
        exception.ParamName.ShouldBe("maxBytes");
    }

    [Fact]
    public void Constructor_WhenMaxBytesZero_AllowsZero()
    {
        new FileReadBounds(0).MaxBytes.ShouldBe(0);
    }
}
