// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryReconcileOperation behavior and contracts.</summary>
public sealed class RecoveryReconcileOperationTests
{
    [Fact]
    public void RecoveryReconcileOperation_Constructor_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryReconcileOperation(null!));
        exception.ParamName.ShouldBe("reference");
    }
}
