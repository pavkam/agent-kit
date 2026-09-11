// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingAttemptCancelled behavior and contracts.</summary>
public sealed class EmbeddingAttemptCancelledTests
{
    [Fact]
    public void EmbeddingAttemptCancelled_Constructor_WhenCancellationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptCancelled(null!));
        exception.ParamName.ShouldBe("cancellation");
    }
}
