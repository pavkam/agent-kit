// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class InMemorySessionStoreTests
{
    [Fact]
    public void Descriptor_WhenAccessed_ReportsNonDurable()
    {
        var store = TestFactory.CreateStore();

        store.Descriptor.Durable.ShouldBeFalse();
        store.Descriptor.Key.Value.ShouldBe("agentkit.in-memory");
    }

    [Fact]
    public void Constructor_WhenSessionIdsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(
            null!,
            new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
            TimeProvider.System));

        exception.ParamName.ShouldBe("sessionIds");
    }

    [Fact]
    public void Constructor_WhenBranchIdsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(
            new GuidIdentifierGenerator<SessionId>(static v => new SessionId(v)),
            null!,
            TimeProvider.System));

        exception.ParamName.ShouldBe("branchIds");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(
            new GuidIdentifierGenerator<SessionId>(static v => new SessionId(v)),
            new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
            null!));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void GuidIdentifierGenerator_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new GuidIdentifierGenerator<SessionId>(null!));

        exception.ParamName.ShouldBe("factory");
    }

    [Fact]
    public void GuidIdentifierGenerator_WhenCreateCalled_ProducesNonDefaultIdentity()
    {
        var generator = new GuidIdentifierGenerator<SessionId>(static v => new SessionId(v));

        var id = generator.Create();

        id.Value.ShouldNotBe(Guid.Empty);
    }
}
