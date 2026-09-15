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
}
