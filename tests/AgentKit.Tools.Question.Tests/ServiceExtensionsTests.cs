// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddQuestionTool_WhenCalledTwice_AddsOneToolAndOneDefaultQuestionIdGenerator()
    {
        var services = new ServiceCollection();
        _ = services.AddQuestionTool().AddQuestionTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(QuestionTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<QuestionId>)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IToolPresentationFormatter)
            && descriptor.ImplementationType == typeof(QuestionToolPresentationFormatter)).ShouldBe(1);
    }

    [Fact]
    public void AddQuestionTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddQuestionTool()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddQuestionTool_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddQuestionTool(static options => options.MaximumPromptCharacters = 10);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<QuestionToolOptions>>().Value.MaximumPromptCharacters.ShouldBe(10);
    }

    [Theory]
    [InlineData(nameof(QuestionToolOptions.DefaultTimeout))]
    [InlineData(nameof(QuestionToolOptions.MaximumTimeout))]
    [InlineData(nameof(QuestionToolOptions.MaximumPromptCharacters))]
    [InlineData(nameof(QuestionToolOptions.MaximumLabelCharacters))]
    [InlineData(nameof(QuestionToolOptions.MaximumDescriptionCharacters))]
    [InlineData(nameof(QuestionToolOptions.MaximumAnswerCharacters))]
    public void AddQuestionTool_WhenBoundIsInvalid_FailsOptionsValidation(string property)
    {
        var services = new ServiceCollection();
        _ = services.AddQuestionTool(options => Apply(options, property));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<QuestionToolOptions>>().Value);
    }

    private static void Apply(QuestionToolOptions options, string property)
    {
        switch (property)
        {
            case nameof(QuestionToolOptions.DefaultTimeout):
                options.DefaultTimeout = TimeSpan.Zero;
                break;
            case nameof(QuestionToolOptions.MaximumTimeout):
                options.MaximumTimeout = options.DefaultTimeout - TimeSpan.FromMinutes(1);
                break;
            case nameof(QuestionToolOptions.MaximumPromptCharacters):
                options.MaximumPromptCharacters = 0;
                break;
            case nameof(QuestionToolOptions.MaximumLabelCharacters):
                options.MaximumLabelCharacters = 0;
                break;
            case nameof(QuestionToolOptions.MaximumDescriptionCharacters):
                options.MaximumDescriptionCharacters = 0;
                break;
            case nameof(QuestionToolOptions.MaximumAnswerCharacters):
                options.MaximumAnswerCharacters = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected option property.");
        }
    }
}
