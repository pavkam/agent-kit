// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceNotFound behavior and contracts.</summary>
public sealed class RecoveryEvidenceNotFoundTests
{
    [Fact]
    public void RecoveryEvidenceNotFound_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryEvidenceNotFound(null!));
        exception.ParamName.ShouldBe("address");
    }
}
