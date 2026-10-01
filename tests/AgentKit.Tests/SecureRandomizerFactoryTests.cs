// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Internal;

/// <summary>Verifies <see cref="SecureRandomizerFactory"/> behavior.</summary>
public sealed class SecureRandomizerFactoryTests
{
    [Fact]
    public void Create_WhenRequestIsValid_ReturnsANondeterministicOperationOwnedRandomizer()
    {
        var factory = new SecureRandomizerFactory();
        var request = new RandomizerCreationRequest(new OperationId(Guid.NewGuid()), new RandomizerPurpose("retry-jitter"));

        var first = factory.Create(request);
        var second = factory.Create(request);

        first.ShouldNotBeSameAs(second);
        first.Descriptor.IsDeterministic.ShouldBeFalse();
        first.Descriptor.SeedFingerprint.ShouldBeNull();
    }

    [Fact]
    public void Create_WhenRequestIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new SecureRandomizerFactory().Create(null!)).ParamName.ShouldBe("request");

    [Fact]
    public void Create_WhenCalledConcurrently_ReturnsAnIndependentRandomizerPerCall()
    {
        var factory = new SecureRandomizerFactory();
        var request = new RandomizerCreationRequest(new OperationId(Guid.NewGuid()), new RandomizerPurpose("parallel"));

        var created = new IRandomizer[64];
        _ = Parallel.For(0, created.Length, index => created[index] = factory.Create(request));

        created.Distinct().Count().ShouldBe(created.Length);
    }
}
