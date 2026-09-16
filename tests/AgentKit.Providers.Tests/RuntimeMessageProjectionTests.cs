// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Verifies the canonical <see cref="RuntimeMessage"/> envelope
/// <see cref="RuntimeMessageProjection"/> builds for every first-party
/// conversational request translator.
/// </summary>
public sealed class RuntimeMessageProjectionTests
{
    private static TextPart Text(string text) => new(text, TextSemantics.Plain, ExtensionData.Empty);

    [Fact]
    public void Format_Always_IsTheStableEnvelopeMarker() =>
        RuntimeMessageProjection.Format.ShouldBe("agentkit.runtime-message.v1");

    [Fact]
    public void BuildEnvelopeJson_WhenPartsIsDefault_ReturnsEnvelopeWithEmptyContent()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson(default);

        var node = JsonNode.Parse(json)!;
        node["format"]!.GetValue<string>().ShouldBe(RuntimeMessageProjection.Format);
        node["content"]!.GetValue<string>().ShouldBe(string.Empty);
    }

    [Fact]
    public void BuildEnvelopeJson_WhenPartsIsEmpty_ReturnsEnvelopeWithEmptyContent()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson([]);

        var node = JsonNode.Parse(json)!;
        node["format"]!.GetValue<string>().ShouldBe(RuntimeMessageProjection.Format);
        node["content"]!.GetValue<string>().ShouldBe(string.Empty);
    }

    [Fact]
    public void BuildEnvelopeJson_WhenSingleTextPart_RoundTripsExactText()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson([Text("The run was interrupted.")]);

        var node = JsonNode.Parse(json)!;
        node["format"]!.GetValue<string>().ShouldBe("agentkit.runtime-message.v1");
        node["content"]!.GetValue<string>().ShouldBe("The run was interrupted.");
    }

    [Fact]
    public void BuildEnvelopeJson_WhenMultipleTextParts_ConcatenatesInSourceOrder()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson([Text("first "), Text("second")]);

        var node = JsonNode.Parse(json)!;
        node["content"]!.GetValue<string>().ShouldBe("first second");
    }

    [Fact]
    public void BuildEnvelopeJson_WhenContentContainsJsonSpecialCharacters_ProducesValidJson()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson([Text("""quote " and backslash \ and newline \n""")]);

        var node = JsonNode.Parse(json)!;
        node["content"]!.GetValue<string>().ShouldBe("""quote " and backslash \ and newline \n""");
    }

    [Fact]
    public void BuildEnvelopeJson_Always_EmitsExactlyTwoPropertiesInFormatThenContentOrder()
    {
        var json = RuntimeMessageProjection.BuildEnvelopeJson([Text("hi")]);

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        properties.ShouldBe(["format", "content"]);
    }

    [Fact]
    public void BuildEnvelopeJson_WhenContentIsNotTextPart_ThrowsNotSupportedException()
    {
        var unsupported = new UnknownContentPart("vendor.special", JsonDocument.Parse("{}").RootElement, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(() => RuntimeMessageProjection.BuildEnvelopeJson([unsupported]));
    }

    [Fact]
    public void BuildEnvelopeJson_WhenEnvelopeIsNotIndistinguishableFromPlainUserText_MarkerIsPresent()
    {
        const string plainUserText = "The run was interrupted.";

        var json = RuntimeMessageProjection.BuildEnvelopeJson([Text(plainUserText)]);

        json.ShouldNotBe(plainUserText);
        json.ShouldContain(RuntimeMessageProjection.Format);
    }
}
