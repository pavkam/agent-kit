// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExternalOperationReference behavior and contracts.</summary>
public sealed class ExternalOperationReferenceTests
{
    [Fact]
    public void ExternalOperationReference_Constructor_WhenHandleIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ExternalOperationReference(new DurableBackendKey("b"), " "));
        exception.ParamName.ShouldBe("handle");
    }
}
