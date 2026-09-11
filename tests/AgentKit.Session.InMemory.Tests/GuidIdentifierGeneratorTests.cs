// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;



/// <summary>Verifies GuidIdentifierGenerator behavior and contracts.</summary>
public sealed class GuidIdentifierGeneratorTests
{
    [Fact]
    public void GuidIdentifierGenerator_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new GuidIdentifierGenerator<SessionId>(null!));
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
