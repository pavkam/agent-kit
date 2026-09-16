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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var backendKey = new DurableBackendKey("b");
        var reference = new ExternalOperationReference(backendKey, "handle-1");
        reference.BackendKey.ShouldBe(backendKey);
        reference.Handle.ShouldBe("handle-1");
    }

    [Fact]
    public void With_WhenHandleIsWhitespace_ThrowsArgumentException()
    {
        var reference = new ExternalOperationReference(new DurableBackendKey("b"), "handle-1");
        var exception = Should.Throw<ArgumentException>(() => _ = reference with { Handle = " " });
        exception.ParamName.ShouldBe("Handle");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExternalOperationReference(new DurableBackendKey("b"), "handle-1");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenValid_UpdatesHandle()
    {
        var original = new ExternalOperationReference(new DurableBackendKey("b"), "handle-1");
        var updated = original with { Handle = "handle-2" };
        updated.Handle.ShouldBe("handle-2");
    }
}
