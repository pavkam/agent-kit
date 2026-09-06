// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;

using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceExtensionsTests
{
    private readonly record struct SampleId
    {
        public SampleId(Guid value) => Value = value;

        public Guid Value { get; }
    }

    private sealed class FirstGenerator: IIdentifierGenerator<SampleId>
    {
        public SampleId Create() => new(Guid.NewGuid());
    }

    private sealed class SecondGenerator: IIdentifierGenerator<SampleId>
    {
        public SampleId Create() => new(Guid.NewGuid());
    }

    [Fact]
    public void TryAddIdentifierGenerator_WhenNoneRegistered_RegistersSingleton()
    {
        var services = new ServiceCollection();

        _ = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IIdentifierGenerator<SampleId>>().ShouldBeOfType<FirstGenerator>();
    }

    [Fact]
    public void TryAddIdentifierGenerator_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();
        _ = services.TryAddIdentifierGenerator<SampleId, SecondGenerator>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IIdentifierGenerator<SampleId>>().ShouldBeOfType<FirstGenerator>();
    }

    [Fact]
    public void TryAddIdentifierGenerator_WhenCalled_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();

        result.ShouldBeSameAs(services);
    }
}
