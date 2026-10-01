// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryProfileRuntimeSelected"/> constraints.</summary>
public sealed class MemoryProfileRuntimeSelectedTests
{
    [Fact]
    public void Constructor_WhenRuntimeIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryProfileRuntimeSelected(null!)).ParamName.ShouldBe("runtime");
}
