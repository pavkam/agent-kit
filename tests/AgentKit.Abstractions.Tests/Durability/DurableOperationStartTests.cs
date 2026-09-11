// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableOperationStart behavior and contracts.</summary>
public sealed class DurableOperationStartTests
{
    [Fact]
    public void DurableOperationStart_Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationStart(null!, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("descriptor");
    }
}
