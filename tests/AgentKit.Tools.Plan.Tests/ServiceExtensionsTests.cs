// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddPlanTool_WhenCalledTwice_RegistersEachDefaultOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddPlanTool().AddPlanTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(PlanTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(TodoTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IPlanStateStore)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<PlanId>)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<SessionEntryId>)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<SecurityEnforcementIntentId>)).ShouldBe(1);
    }

    [Fact]
    public void AddPlanTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddPlanTool()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddPlanTool_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddPlanTool(static options => options.MaximumItems = 10);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<PlanToolOptions>>().Value.MaximumItems.ShouldBe(10);
    }

    [Theory]
    [InlineData(nameof(PlanToolOptions.MaximumTitleCharacters), 0)]
    [InlineData(nameof(PlanToolOptions.MaximumItemCharacters), 0)]
    [InlineData(nameof(PlanToolOptions.MaximumItemIdCharacters), 0)]
    [InlineData(nameof(PlanToolOptions.MaximumItems), 0)]
    [InlineData(nameof(PlanToolOptions.MaximumItems), 51)]
    public void AddPlanTool_WhenBoundIsInvalid_FailsOptionsValidation(string property, int invalidValue)
    {
        var services = new ServiceCollection();
        _ = services.AddPlanTool(options => Apply(options, property, invalidValue));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PlanToolOptions>>().Value);
    }

    private static void Apply(PlanToolOptions options, string property, int value)
    {
        switch (property)
        {
            case nameof(PlanToolOptions.MaximumTitleCharacters):
                options.MaximumTitleCharacters = value;
                break;
            case nameof(PlanToolOptions.MaximumItemCharacters):
                options.MaximumItemCharacters = value;
                break;
            case nameof(PlanToolOptions.MaximumItemIdCharacters):
                options.MaximumItemIdCharacters = value;
                break;
            case nameof(PlanToolOptions.MaximumItems):
                options.MaximumItems = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected option property.");
        }
    }
}
