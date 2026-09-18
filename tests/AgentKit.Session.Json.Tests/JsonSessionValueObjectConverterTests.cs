// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using System.Text.Json;

/// <summary>Verifies the reflection-free-at-use-site converter that routes decoded identities back through their own validating constructor.</summary>
public sealed class JsonSessionValueObjectConverterTests
{
    /// <summary>Verifies a value round-trips through its own validating constructor.</summary>
    [Fact]
    public void ReadAndWrite_WhenValueIsValid_RoundTripsThroughTheConstructor()
    {
        var options = Options();
        var agentId = new AgentId(Guid.NewGuid());

        var persisted = JsonSerializer.Serialize(agentId, options);
        var reopened = JsonSerializer.Deserialize<AgentId>(persisted, options);

        reopened.ShouldBe(agentId);
    }

    /// <summary>Verifies the emitted payload is a single-property object using the configured naming policy.</summary>
    [Fact]
    public void Write_WhenCalled_EmitsOneObjectPropertyNamedByPolicy()
    {
        var options = Options();
        var agentId = new AgentId(Guid.NewGuid());

        var persisted = JsonSerializer.Serialize(agentId, options);

        using var document = JsonDocument.Parse(persisted);
        var property = document.RootElement.EnumerateObject().ShouldHaveSingleItem();
        property.Name.ShouldBe("Value");
    }

    /// <summary>Verifies a non-object payload is rejected as malformed rather than coerced.</summary>
    [Fact]
    public void Read_WhenPayloadIsNotAnObject_ThrowsJsonException() =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AgentId>("42", Options()));

    /// <summary>Verifies a payload omitting the single value property is rejected.</summary>
    [Fact]
    public void Read_WhenValuePropertyIsMissing_ThrowsJsonException() =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AgentId>("{}", Options()));

    /// <summary>Verifies a payload whose value property is explicitly null is rejected rather than passed to the constructor.</summary>
    [Fact]
    public void Read_WhenValuePropertyIsNull_ThrowsJsonException() =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AgentId>("""{"Value":null}""", Options()));

    /// <summary>Verifies a value the identity's own constructor rejects surfaces that constructor's exact exception.</summary>
    [Fact]
    public void Read_WhenConstructorRejectsTheValue_ThrowsTheConstructorException() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            JsonSerializer.Deserialize<AgentId>($$"""{"Value":"{{Guid.Empty}}"}""", Options()));

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonSessionValueObjectConverterFactory());
        return options;
    }
}
