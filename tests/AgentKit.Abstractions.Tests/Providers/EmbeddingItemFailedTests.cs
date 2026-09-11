// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingItemFailed behavior and contracts.</summary>
public sealed class EmbeddingItemFailedTests
{
    [Fact]
    public void EmbeddingItemFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingItemFailed(0, null, null!));
        exception.ParamName.ShouldBe("failure");
    }
}
