// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingAttemptCompleted behavior and contracts.</summary>
public sealed class EmbeddingAttemptCompletedTests
{
    [Fact]
    public void EmbeddingAttemptCompleted_Constructor_WhenResponseNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptCompleted(null!));
        exception.ParamName.ShouldBe("response");
    }
}
