// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddContextCompaction_WhenCalled_RegistersCompactorAndCollaborators()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
        _ = provider.GetRequiredService<ICompactionCutSelector>().ShouldBeOfType<StructuralCompactionCutSelector>();
        _ = provider.GetRequiredService<ICompactionStrategy>().ShouldBeOfType<ExtractiveCompactionStrategy>();
        _ = provider.GetRequiredService<ICompactionValidator>().ShouldBeOfType<DefaultCompactionValidator>();
        _ = provider.GetRequiredService<ICompactionSizeEstimator>().ShouldBeOfType<CharacterCompactionSizeEstimator>();
    }

    [Fact]
    public void AddContextCompaction_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction();
        _ = services.AddContextCompaction();
        WithFakeCoordinator(services);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ICompactor>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddContextCompaction_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddContextCompaction(o => o.MaximumSourceEntries = 42);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.MaximumSourceEntries.ShouldBe(42);
    }

    [Fact]
    public void AddContextCompaction_WhenMaximumSourceEntriesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumSourceEntries = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Fact]
    public void AddContextCompaction_WhenCharactersPerTokenIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.CharactersPerToken = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    public void AddContextCompaction_WhenMaximumCheckpointCharactersDoesNotExceedTruncationMarker_FailsValidationOnAccess(int maximum)
    {
        // A ceiling at or below the marker length would make the extractive strategy return untruncated text and the
        // validator reject it on every attempt: a deterministically failing pipeline that must fail at composition.
        ExtractiveCompactionStrategy.TruncationMarker.Length.ShouldBe(19);
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumCheckpointCharacters = maximum);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompactionOptions>>().Value);
    }

    [Fact]
    public void AddContextCompaction_WhenMaximumCheckpointCharactersJustExceedsTruncationMarker_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddContextCompaction(o => o.MaximumCheckpointCharacters = ExtractiveCompactionStrategy.TruncationMarker.Length + 1);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CompactionOptions>>().Value.MaximumCheckpointCharacters.ShouldBe(20);
    }

    private static void WithFakeCoordinator(IServiceCollection services) =>
        services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
}
