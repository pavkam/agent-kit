// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceLoaded behavior and contracts.</summary>
public sealed class RecoveryEvidenceLoadedTests
{
    [Fact]
    public void RecoveryEvidenceLoaded_Constructor_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryEvidenceLoaded(null!));
        exception.ParamName.ShouldBe("evidence");
    }
}
