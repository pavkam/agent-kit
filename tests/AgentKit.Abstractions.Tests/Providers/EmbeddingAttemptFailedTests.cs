// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingAttemptFailed behavior and contracts.</summary>
public sealed class EmbeddingAttemptFailedTests
{
    [Fact]
    public void EmbeddingAttemptFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }
}
