// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddResourceTool_WhenCalledTwice_AddsOneToolDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddResourceTool().AddResourceTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(ResourceTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IToolPresentationFormatter)
            && descriptor.ImplementationType == typeof(ResourceToolPresentationFormatter)).ShouldBe(1);
    }

    [Fact]
    public void AddResourceTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddResourceTool()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddResourceTool_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddResourceTool(static options => options.MaximumBytes = 42);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ResourceToolOptions>>().Value.MaximumBytes.ShouldBe(42);
    }

    [Theory]
    [InlineData(nameof(ResourceToolOptions.MaximumBytes))]
    [InlineData(nameof(ResourceToolOptions.MaximumCharacters))]
    [InlineData(nameof(ResourceToolOptions.MaximumDescriptionCharacters))]
    public void AddResourceTool_WhenBoundIsNotPositive_FailsOptionsValidation(string property)
    {
        var services = new ServiceCollection();
        _ = services.AddResourceTool(options => Apply(options, property));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ResourceToolOptions>>().Value);
    }

    [Fact]
    public void AddResourceTool_WhenResourceIdsAreDuplicated_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddResourceTool(options =>
        {
            options.Resources.Add(Definition("dup"));
            options.Resources.Add(Definition("dup"));
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ResourceToolOptions>>().Value);
    }

    private static FileResourceDefinition Definition(string id) => new(
        new ResourceId(id),
        ResourceKind.Documentation,
        ResourceTrust.Managed,
        "description",
        new FileSystemPath("a.txt"),
        "text/plain");

    private static void Apply(ResourceToolOptions options, string property)
    {
        switch (property)
        {
            case nameof(ResourceToolOptions.MaximumBytes):
                options.MaximumBytes = 0;
                break;
            case nameof(ResourceToolOptions.MaximumCharacters):
                options.MaximumCharacters = 0;
                break;
            case nameof(ResourceToolOptions.MaximumDescriptionCharacters):
                options.MaximumDescriptionCharacters = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected option property.");
        }
    }
}
