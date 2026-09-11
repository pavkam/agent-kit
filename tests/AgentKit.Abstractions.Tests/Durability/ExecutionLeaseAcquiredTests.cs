// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExecutionLeaseAcquired behavior and contracts.</summary>
public sealed class ExecutionLeaseAcquiredTests
{
    [Fact]
    public void ExecutionLeaseAcquired_Constructor_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExecutionLeaseAcquired(null!));
        exception.ParamName.ShouldBe("lease");
    }
}
