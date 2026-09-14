// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolDescriptorExtensionsTests
{
    [Fact]
    public void ToLlmToolDefinition_WhenDescriptorNull_ThrowsArgumentNullException()
    {
        ToolDescriptor? descriptor = null;

        var exception = Should.Throw<ArgumentNullException>(() => descriptor!.ToLlmToolDefinition());

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void ToLlmToolDefinition_WhenDescriptorValid_ProjectsIdentityNameDescriptionAndSchema()
    {
        var descriptor = TestFactory.Descriptor("read_file");

        var definition = descriptor.ToLlmToolDefinition();

        definition.Id.ShouldBe(descriptor.Id);
        definition.Name.ShouldBe(descriptor.Name);
        definition.Description.ShouldBe(descriptor.Description);
        JsonElement.DeepEquals(definition.ParametersSchema, descriptor.InputSchema.Document).ShouldBeTrue();
    }

    [Fact]
    public void ToLlmToolDefinitions_WhenDescriptorsNull_ThrowsArgumentNullException()
    {
        IEnumerable<ToolDescriptor>? descriptors = null;

        var exception = Should.Throw<ArgumentNullException>(() => descriptors!.ToLlmToolDefinitions());

        exception.ParamName.ShouldBe("descriptors");
    }

    [Fact]
    public void ToLlmToolDefinitions_WhenDescriptorsProvided_ProjectsEachInOrder()
    {
        ToolDescriptor[] descriptors = [TestFactory.Descriptor("first"), TestFactory.Descriptor("second")];

        var definitions = descriptors.ToLlmToolDefinitions();

        definitions.Length.ShouldBe(2);
        definitions[0].Id.ShouldBe(descriptors[0].Id);
        definitions[1].Id.ShouldBe(descriptors[1].Id);
    }
}
