// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionStoreDescriptor behavior and contracts.</summary>
public sealed class SessionStoreDescriptorTests
{
    [Fact]
    public void Constructor_WhenKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreDescriptor(default, SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenCapabilitiesContainUnknownFlag_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreDescriptor(new SessionStoreKey("store"), (SessionStoreCapabilities) 16, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void Constructor_WhenConsistencyIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.None, (SessionConsistencyModel) 42, durable: false, supportsDistributedFencing: false));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("consistency");
    }

    [Fact]
    public void With_WhenKeyIsBlank_ThrowsExactArgumentException()
    {
        var descriptor = Descriptor();
        var exception = Should.Throw<ArgumentNullException>(() => descriptor with { Key = default });
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ObjectInitializer_WhenCapabilityIsUnknown_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false) { Capabilities = (SessionStoreCapabilities) 16, });
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenConsistencyIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var descriptor = Descriptor();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => descriptor with { Consistency = (SessionConsistencyModel) 42 });
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    private static SessionStoreDescriptor Descriptor() => new(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false);

    [Fact]
    public void Properties_WhenRead_ReturnConstructedValues()
    {
        var descriptor = new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.Branching, SessionConsistencyModel.Strong, durable: true, supportsDistributedFencing: true);
        descriptor.Key.ShouldBe(new SessionStoreKey("store"));
        descriptor.Capabilities.ShouldBe(SessionStoreCapabilities.Branching);
        descriptor.Consistency.ShouldBe(SessionConsistencyModel.Strong);
    }
    [Fact]
    public void SessionStoreDescriptor_Constructor_WhenDurableTrueOrFalse_RoundTrips()
    {
        var descriptor = new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: true, supportsDistributedFencing: false);
        descriptor.Durable.ShouldBeTrue();
    }

    [Fact]
    public void SessionStoreDescriptor_Equality_WhenSameValues_InstancesAreEqual() => new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, true, false).ShouldBe(new SessionStoreDescriptor(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, true, false));
    [Fact]
    public void SessionStoreDescriptor_WhenCapabilitiesContainUnknownFlag_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreDescriptor(new SessionStoreKey("store"), (SessionStoreCapabilities) 16, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("capabilities");
    }
}
