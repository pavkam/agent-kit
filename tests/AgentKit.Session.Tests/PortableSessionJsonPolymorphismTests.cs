// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>Verifies the shared durable discriminator map is strict, complete, and applied only to the closed hierarchies.</summary>
public sealed class PortableSessionJsonPolymorphismTests
{
    [Fact]
    public void Apply_WhenTypeInfoIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => PortableSessionJsonPolymorphism.Apply(null!));

        exception.ParamName.ShouldBe("typeInfo");
    }

    [Fact]
    public void Apply_WhenTypeIsNotPolymorphicBase_LeavesContractUntouched()
    {
        var typeInfo = new DefaultJsonTypeInfoResolver().GetTypeInfo(typeof(TextPart), new JsonSerializerOptions());

        PortableSessionJsonPolymorphism.Apply(typeInfo);

        typeInfo.PolymorphismOptions.ShouldBeNull();
    }

    [Theory]
    [InlineData(typeof(ContentPart))]
    [InlineData(typeof(AgentMessage))]
    [InlineData(typeof(OperationCorrelation))]
    public void Apply_WhenTypeIsPolymorphicBase_AssignsStrictKindDiscriminator(Type baseType)
    {
        var typeInfo = new DefaultJsonTypeInfoResolver().GetTypeInfo(baseType, new JsonSerializerOptions());

        PortableSessionJsonPolymorphism.Apply(typeInfo);

        var options = typeInfo.PolymorphismOptions.ShouldNotBeNull();
        options.TypeDiscriminatorPropertyName.ShouldBe("$kind");
        options.IgnoreUnrecognizedTypeDiscriminators.ShouldBeFalse();
        options.UnknownDerivedTypeHandling.ShouldBe(JsonUnknownDerivedTypeHandling.FailSerialization);
        options.DerivedTypes.ShouldAllBe(derived => derived.DerivedType.IsAssignableTo(baseType));
    }

    [Fact]
    public void Parts_WhenCalled_ListsEveryConcreteContentPartExactlyOnce()
    {
        var expected = typeof(ContentPart).Assembly.GetTypes()
            .Where(static type => type.IsAssignableTo(typeof(ContentPart)) && !type.IsAbstract)
            .ToHashSet();

        var actual = PortableSessionJsonPolymorphism.Parts().DerivedTypes.Select(static derived => derived.DerivedType).ToArray();

        actual.ShouldBe(expected, ignoreOrder: true);
        actual.Length.ShouldBe(expected.Count);
        PortableSessionJsonPolymorphism.Parts().DerivedTypes
            .Single(static derived => derived.DerivedType == typeof(StructuredDataPart)).TypeDiscriminator.ShouldBe("structured");
    }

    [Fact]
    public void Messages_WhenCalled_ListsEveryConcreteAgentMessageExactlyOnce()
    {
        var expected = typeof(AgentMessage).Assembly.GetTypes()
            .Where(static type => type.IsAssignableTo(typeof(AgentMessage)) && !type.IsAbstract)
            .ToHashSet();

        var actual = PortableSessionJsonPolymorphism.Messages().DerivedTypes.Select(static derived => derived.DerivedType).ToArray();

        actual.ShouldBe(expected, ignoreOrder: true);
        actual.Length.ShouldBe(expected.Count);
    }

    [Fact]
    public void Correlations_WhenCalled_ListsEveryConcreteCorrelationExactlyOnce()
    {
        var expected = typeof(OperationCorrelation).Assembly.GetTypes()
            .Where(static type => type.IsAssignableTo(typeof(OperationCorrelation)) && !type.IsAbstract)
            .ToHashSet();

        var actual = PortableSessionJsonPolymorphism.Correlations().DerivedTypes.Select(static derived => derived.DerivedType).ToArray();

        actual.ShouldBe(expected, ignoreOrder: true);
        actual.Length.ShouldBe(expected.Count);
    }

    [Fact]
    public void Parts_WhenCalledTwice_ReturnsIndependentInstances() =>
        PortableSessionJsonPolymorphism.Parts().ShouldNotBeSameAs(PortableSessionJsonPolymorphism.Parts());

    [Fact]
    public void CreateResolver_WhenDiscriminatorIsUnknown_FailsDeserialization()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = PortableSessionJsonPolymorphism.CreateResolver() };

        _ = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ContentPart>("""{"$kind":"json","Value":{}}""", options));
    }
}
