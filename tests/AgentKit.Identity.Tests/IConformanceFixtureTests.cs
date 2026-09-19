// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Verifies <see cref="IConformanceFixture{TContract}"/>.</summary>
public sealed class IConformanceFixtureTests
{
    [Fact]
    public async Task CreateAsync_WhenCapabilitiesAreDeclaredFirst_ReturnsTheSubjectWithoutHidingThem()
    {
        await using var fixture = new StubFixture();
        fixture.Capabilities.SupportsDurability.ShouldBeFalse();
        fixture.Capabilities.SupportsConcurrentCreators.ShouldBeTrue();

        var subject = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        subject.Name.ShouldBe("stub");
    }

    private sealed class StubFixture: IConformanceFixture<StubContract>
    {
        public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

        public ValueTask<StubContract> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(new StubContract("stub"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record StubContract(string Name);
}
