// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryCommitRecordedResult behavior and contracts.</summary>
public sealed class RecoveryCommitRecordedResultTests
{
    [Fact]
    public void RecoveryCommitRecordedResult_Constructor_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryCommitRecordedResult(null!));
        exception.ParamName.ShouldBe("result");
    }
}
