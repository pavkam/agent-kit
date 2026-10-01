// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryStoreSelectionRequest"/> constraints.</summary>
public sealed class MemoryStoreSelectionRequestTests
{
    [Fact]
    public void Constructor_WhenKeyIsSupplied_PreservesIt() =>
        new MemoryStoreSelectionRequest(new MemoryStoreKey("k")).Key.ShouldBe(new MemoryStoreKey("k"));

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryStoreSelectionRequest(default)).ParamName.ShouldBe("key");
}
