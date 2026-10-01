// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval.Tests;

/// <summary>Verifies registration behavior and option validation.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddRetrievalContextContributor_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddRetrievalContextContributor(new ComponentKey<IContextAssembler>("assembler"))).ParamName.ShouldBe("services");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddRetrievalContextContributor_WhenACeilingIsNotPositive_FailsOptionValidation(int value)
    {
        var services = new ServiceCollection();
        _ = services.AddRetrievalContextContributor(new ComponentKey<IContextAssembler>("assembler"), options => options.MaximumItems = value);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RetrievalContextOptions>>().Value);
    }

    [Fact]
    public void AddRetrievalContextContributor_WhenClassificationIsUndefined_FailsOptionValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddRetrievalContextContributor(new ComponentKey<IContextAssembler>("assembler"), options => options.MaximumClassification = (DataClassification) 99);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RetrievalContextOptions>>().Value);
    }

    [Fact]
    public void AddRetrievalContextContributor_WhenDefaults_UseInternalClassificationAndEightItems()
    {
        var services = new ServiceCollection();
        _ = services.AddRetrievalContextContributor(new ComponentKey<IContextAssembler>("assembler"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RetrievalContextOptions>>().Value;

        options.MaximumClassification.ShouldBe(DataClassification.Internal);
        options.MaximumItems.ShouldBe(8);
    }

    [Fact]
    public void AddRetrievalContextContributor_WhenAssemblerKeyIsDefault_Throws() =>
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddRetrievalContextContributor(default));
}
