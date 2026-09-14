// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies GuidIdentifierGenerator behavior and contracts.</summary>
/// <remarks>Exercised directly: <c>AgentKit.Conversations</c> grants this assembly <c>InternalsVisibleTo</c>.</remarks>
public sealed class GuidIdentifierGeneratorTests
{
    [Fact]
    public void Constructor_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new GuidIdentifierGenerator<RunId>(null!));
        exception.ParamName.ShouldBe("factory");
    }

    [Fact]
    public void Create_WhenCalledRepeatedly_InvokesFactoryWithFreshNonEmptyGuidsEachTime()
    {
        var seen = new List<Guid>();
        var generator = new GuidIdentifierGenerator<RunId>(guid =>
        {
            seen.Add(guid);
            return new RunId(guid);
        });

        var first = generator.Create();
        var second = generator.Create();

        seen.Count.ShouldBe(2);
        seen[0].ShouldNotBe(Guid.Empty);
        seen[1].ShouldNotBe(Guid.Empty);
        seen[0].ShouldNotBe(seen[1]);
        first.ShouldNotBe(second);
        first.Value.ShouldBe(seen[0]);
        second.Value.ShouldBe(seen[1]);
    }
}
