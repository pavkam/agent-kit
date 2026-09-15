// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Verifies the shared extension passthrough and tool-argument parsing rules in <see cref="ProviderJson"/>.</summary>
public sealed class ProviderJsonTests
{
    private static ExtensionData Extensions(params (string Key, string Json)[] entries) =>
        new(entries.Aggregate(
            ImmutableDictionary<string, ExtensionValue>.Empty,
            (map, entry) => map.Add(entry.Key, new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(JsonNode.Parse(entry.Json))]))));

    [Fact]
    public void ApplyExtensions_WhenBodyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderJson.ApplyExtensions(null!, ExtensionData.Empty));

        exception.ParamName.ShouldBe("body");
    }

    [Fact]
    public void ApplyExtensions_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderJson.ApplyExtensions([], null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ApplyExtensions_WhenExtensionsIsEmpty_LeavesBodyUnchanged()
    {
        var body = new JsonObject { ["model"] = "m" };

        ProviderJson.ApplyExtensions(body, ExtensionData.Empty);

        body.ToJsonString().ShouldBe( /*lang=json,strict*/"""{"model":"m"}""");
    }

    [Fact]
    public void ApplyExtensions_WhenKeyIsAbsent_AttachesParsedNode()
    {
        var body = new JsonObject { ["model"] = "m" };

        ProviderJson.ApplyExtensions(body, Extensions(("logprobs", "true"), ("metadata", /*lang=json,strict*/ """{"user":"u","tags":[1,2]}""")));

        body["logprobs"]!.GetValue<bool>().ShouldBeTrue();
        body["metadata"]!["user"]!.GetValue<string>().ShouldBe("u");
        body["metadata"]!["tags"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyExtensions_WhenKeyAlreadyExists_DoesNotOverrideOwnedField()
    {
        var body = new JsonObject { ["model"] = "owned" };

        ProviderJson.ApplyExtensions(body, Extensions(("model", "\"hijacked\""), ("extra", "1")));

        body["model"]!.GetValue<string>().ShouldBe("owned");
        body["extra"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void ApplyExtensions_WhenValueIsJsonNull_AttachesNullNode()
    {
        var body = new JsonObject();

        ProviderJson.ApplyExtensions(body, Extensions(("stop", "null")));

        body.ContainsKey("stop").ShouldBeTrue();
        body["stop"].ShouldBeNull();
    }

    [Fact]
    public void ParseEmptyObject_Always_ReturnsDetachedEmptyObject()
    {
        var element = ProviderJson.ParseEmptyObject();

        element.ValueKind.ShouldBe(JsonValueKind.Object);
        element.EnumerateObject().Count().ShouldBe(0);
        element.GetRawText().ShouldBe("{}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseArguments_WhenJsonIsAbsent_ReturnsEmptyObject(string? json)
    {
        var element = ProviderJson.ParseArguments(json);

        element.ValueKind.ShouldBe(JsonValueKind.Object);
        element.GetRawText().ShouldBe("{}");
    }

    [Fact]
    public void ParseArguments_WhenJsonIsValid_ReturnsDetachedRootElement()
    {
        var element = ProviderJson.ParseArguments( /*lang=json,strict*/"""{"location":"Paris","units":["c"]}""");

        element.GetProperty("location").GetString().ShouldBe("Paris");
        element.GetProperty("units")[0].GetString().ShouldBe("c");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("{ not json")]
    [InlineData("{\"a\":")]
    public void ParseArguments_WhenJsonIsMalformedOrWhitespace_ThrowsJsonException(string json) =>
        _ = Should.Throw<JsonException>(() => ProviderJson.ParseArguments(json));
}
