// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using System.Text;
using System.Text.Json;

/// <summary>Verifies KnownModelCatalog behavior and the integrity of the embedded known-models resource.</summary>
public sealed class KnownModelCatalogTests
{
    /// <summary>Every provider identifier a first-party adapter package registers; the catalog may not name anything else.</summary>
    private static readonly ImmutableHashSet<string> _adapterProviderIds =
    [
        "anthropic", "aws-bedrock", "azure-openai", "cohere", "deepseek", "google-gemini", "google-vertex-ai",
        "groq", "mistral-ai", "moonshot-kimi", "ollama", "openai", "openrouter", "xai", "z-ai",
    ];

    [Fact]
    public void Default_WhenAccessed_LoadsTheEmbeddedResourceOnce()
    {
        var first = KnownModelCatalog.Default;
        var second = KnownModelCatalog.Default;

        first.ShouldBeSameAs(second);
        first.Models.Length.ShouldBeGreaterThan(100);
        first.Provenance.SourceName.ShouldBe("openclaw/catalog");
        first.Provenance.SourceUrl.ShouldBe(new Uri("https://catalog.openclaw.ai/models/v1/catalog.json"));
        first.Provenance.SourceCommit.ShouldNotBeNullOrWhiteSpace();
        first.Provenance.ImportedAt.ShouldBeGreaterThanOrEqualTo(first.Provenance.GeneratedAt);
    }

    [Fact]
    public void Default_WhenInspected_NamesOnlyProvidersWithAFirstPartyAdapter()
    {
        var unknown = KnownModelCatalog.Default.Providers.Select(static p => p.Value).Where(p => !_adapterProviderIds.Contains(p)).ToArray();

        unknown.ShouldBeEmpty();
        KnownModelCatalog.Default.Providers.Count().ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Default_WhenInspected_EveryEntryIsInternallyConsistent()
    {
        foreach (var model in KnownModelCatalog.Default.Models)
        {
            model.SupportsToolCalls.ShouldBeTrue($"{model.ProviderId.Value}/{model.ModelId.Value} is not a tool-calling model");
            _ = model.Limits.MaxContextTokens.ShouldNotBeNull($"{model.ModelId.Value} has no context window");
            if (model.Limits is { MaxContextTokens: { } context, MaxOutputTokens: { } output })
            {
                output.ShouldBeLessThanOrEqualTo(context * 4, $"{model.ModelId.Value} output limit is implausible");
            }

            if (model.Pricing is { } pricing)
            {
                pricing.Currency.ShouldBe("USD");
                (pricing.InputPerMillionTokens ?? 0).ShouldBeLessThan(1000m);
                (pricing.OutputPerMillionTokens ?? 0).ShouldBeLessThan(1000m);
            }

            if (model.ReplacedBy is { } successor)
            {
                model.Status.ShouldBe(KnownModelStatus.Deprecated, $"{model.ModelId.Value} names a successor but is not deprecated");
                successor.Value.ShouldNotBe(model.ModelId.Value);
            }
        }
    }

    [Theory]
    [InlineData("openai", "gpt-4o-mini")]
    [InlineData("anthropic", "claude-sonnet-4-5")]
    [InlineData("google-gemini", "gemini-2.5-flash")]
    public void TryFind_WhenModelIsKnown_ReturnsItWithLimitsAndPricing(string providerId, string modelId)
    {
        KnownModelCatalog.Default.TryFind(new ProviderId(providerId), new ModelId(modelId), out var model).ShouldBeTrue();

        _ = model.ShouldNotBeNull();
        model.DisplayName.ShouldNotBeNullOrWhiteSpace();
        _ = model.Limits.MaxContextTokens.ShouldNotBeNull();
        _ = model.Pricing.ShouldNotBeNull().InputPerMillionTokens.ShouldNotBeNull();
    }

    [Fact]
    public void TryFind_WhenModelIsUnknown_ReturnsFalse()
    {
        KnownModelCatalog.Default.TryFind(new ProviderId("openai"), new ModelId("no-such-model"), out var model).ShouldBeFalse();
        model.ShouldBeNull();
    }

    [Fact]
    public void ForProvider_WhenProviderHasModels_ReturnsOnlyThatProvidersModels()
    {
        var models = KnownModelCatalog.Default.ForProvider(new ProviderId("openai"));

        models.ShouldNotBeEmpty();
        models.ShouldAllBe(static model => model.ProviderId == new ProviderId("openai"));
    }

    [Fact]
    public void ToDescriptor_WhenCalled_OverlaysModelFactsOnTheProviderBaseline()
    {
        _ = KnownModelCatalog.Default.TryFind(new ProviderId("openai"), new ModelId("gpt-5"), out var model);
        var baseline = new ModelCapabilities(
            supportsSystemInstructions: true, supportsStreaming: true, supportsToolCalls: false, supportsParallelToolCalls: true,
            supportsStructuredOutput: true, supportsReasoning: false, supportsVisionInput: false, ExtensionData.Empty);

        var descriptor = model!.ToDescriptor(new ModelAlias("smart"), new ApiFamilyId("openai-chat-completions"), baseline);

        descriptor.Alias.ShouldBe(new ModelAlias("smart"));
        descriptor.ProviderId.ShouldBe(new ProviderId("openai"));
        descriptor.ModelId.ShouldBe(new ModelId("gpt-5"));
        descriptor.Capabilities.SupportsStreaming.ShouldBeTrue();
        descriptor.Capabilities.SupportsStructuredOutput.ShouldBeTrue();
        descriptor.Capabilities.SupportsToolCalls.ShouldBeTrue();
        descriptor.Capabilities.SupportsParallelToolCalls.ShouldBeTrue();
        descriptor.Capabilities.SupportsReasoning.ShouldBeTrue();
        descriptor.Capabilities.SupportsVisionInput.ShouldBeTrue();
        descriptor.Limits.ShouldBe(model.Limits);
        descriptor.Pricing.ShouldNotBeNull().InputCostPerMillionTokens.ShouldBe(model.Pricing!.InputPerMillionTokens);
        descriptor.Pricing.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public async Task Default_WhenAccessedConcurrently_ReturnsOneInstance()
    {
        var instances = await Task.WhenAll(Enumerable.Range(0, 16).Select(static _ => Task.Run(static () => KnownModelCatalog.Default)));

        instances.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void Default_WhenInspected_ModelsAreSortedByProviderThenModelId()
    {
        // The import script sorts deterministically so the resource diff reads as a changelog; a hand edit that breaks
        // the order would be the first sign the file was not regenerated.
        var keys = KnownModelCatalog.Default.Models.Select(static m => (m.ProviderId.Value, m.ModelId.Value)).ToArray();

        keys.ShouldBe([.. keys.OrderBy(static k => k.Item1, StringComparer.Ordinal).ThenBy(static k => k.Item2, StringComparer.Ordinal)]);
    }

    [Fact]
    public void Default_WhenInspected_ContainsNoDisabledModelsAndSomeDeprecatedOnes()
    {
        // A disabled model is retired upstream and is not imported; if that changes, the docs and picker semantics must too.
        KnownModelCatalog.Default.Models.ShouldAllBe(static m => m.Status != KnownModelStatus.Disabled);
        KnownModelCatalog.Default.Models.ShouldContain(static m => m.Status == KnownModelStatus.Deprecated);
    }

    [Fact]
    public void TryFind_WhenModelIdDiffersOnlyByCase_ReturnsFalse() =>
        KnownModelCatalog.Default.TryFind(new ProviderId("openai"), new ModelId("GPT-4O-MINI"), out _).ShouldBeFalse();

    [Fact]
    public void ForProvider_WhenProviderIsUnknown_ReturnsEmpty() =>
        KnownModelCatalog.Default.ForProvider(new ProviderId("no-such-provider")).ShouldBeEmpty();

    [Fact]
    public void Providers_WhenEnumerated_ListsEachProviderOnceInCatalogOrder()
    {
        var providers = KnownModelCatalog.Default.Providers.Select(static p => p.Value).ToArray();

        providers.Distinct().Count().ShouldBe(providers.Length);
        providers.ShouldBe([.. providers.OrderBy(static p => p, StringComparer.Ordinal)]);
    }

    [Fact]
    public void Constructor_WhenProvenanceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new KnownModelCatalog(null!, [])).ParamName.ShouldBe("provenance");

    [Fact]
    public void Constructor_WhenModelsIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new KnownModelCatalog(KnownModelCatalog.Default.Provenance, default)).ParamName.ShouldBe("models");

    [Fact]
    public void Constructor_WhenModelsContainsNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new KnownModelCatalog(KnownModelCatalog.Default.Provenance, [KnownModelCatalog.Default.Models[0], null!])).ParamName.ShouldBe("models");

    [Fact]
    public void Constructor_WhenModelsIsEmpty_CreatesAnEmptyCatalog()
    {
        var catalog = new KnownModelCatalog(KnownModelCatalog.Default.Provenance, []);

        catalog.Models.ShouldBeEmpty();
        catalog.Providers.ShouldBeEmpty();
        catalog.TryFind(new ProviderId("openai"), new ModelId("gpt-4o"), out _).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenSameModelIdIsServedByTwoProviders_AcceptsBoth()
    {
        var openAi = KnownModelCatalog.Default.Models.First(static m => m.ProviderId.Value == "openai");
        var elsewhere = new KnownModel(new ProviderId("groq"), openAi.ModelId, openAi.DisplayName, openAi.Status, openAi.SupportsReasoning, openAi.SupportsVisionInput, openAi.SupportsToolCalls, openAi.Limits, null, null);

        var catalog = new KnownModelCatalog(KnownModelCatalog.Default.Provenance, [openAi, elsewhere]);

        catalog.TryFind(new ProviderId("openai"), openAi.ModelId, out var first).ShouldBeTrue();
        catalog.TryFind(new ProviderId("groq"), openAi.ModelId, out var second).ShouldBeTrue();
        first.ShouldBeSameAs(openAi);
        second.ShouldBeSameAs(elsewhere);
    }

    [Fact]
    public void Constructor_WhenTwoModelsShareProviderAndId_ThrowsArgumentException()
    {
        var model = KnownModelCatalog.Default.Models[0];

        var exception = Should.Throw<ArgumentException>(() => new KnownModelCatalog(KnownModelCatalog.Default.Provenance, [model, model]));

        exception.ParamName.ShouldBe("models");
    }

    [Fact]
    public void Parse_WhenSchemaVersionIsUnsupported_ThrowsInvalidOperationException()
    {
        var json = /*lang=json,strict*/ """{"schemaVersion":2,"source":{"name":"x","url":"https://example.test/c.json","generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-01T00:00:00Z"},"models":[]}""";

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Parse_WhenStatusIsUnknown_ThrowsJsonException()
    {
        var json = /*lang=json,strict*/ """{"schemaVersion":1,"source":{"name":"x","url":"https://example.test/c.json","generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-01T00:00:00Z"},"models":[{"providerId":"openai","modelId":"m","displayName":"M","status":"retired"}]}""";

        _ = Should.Throw<JsonException>(() => KnownModelCatalog.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Parse_WhenPricingIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var json = /*lang=json,strict*/ """{"schemaVersion":1,"source":{"name":"x","url":"https://example.test/c.json","generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-01T00:00:00Z"},"models":[{"providerId":"openai","modelId":"m","displayName":"M","status":"available","pricing":{"currency":"USD","inputPerMillionTokens":-1}}]}""";

        _ = Should.Throw<ArgumentOutOfRangeException>(() => KnownModelCatalog.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Parse_WhenBytesAreNotJson_ThrowsJsonException() =>
        Should.Throw<JsonException>(() => KnownModelCatalog.Parse("not json"u8));

    [Fact]
    public void Parse_WhenDocumentIsJsonNull_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse("null"u8));

    [Theory]
    [InlineData("source")]
    [InlineData("models")]
    public void Parse_WhenARequiredTopLevelMemberIsMissing_ThrowsInvalidOperationException(string member)
    {
        using var parsed = JsonDocument.Parse(Valid);
        var trimmed = parsed.RootElement.EnumerateObject().Where(p => p.Name != member).ToDictionary(static p => p.Name, static p => p.Value.Clone());

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(JsonSerializer.SerializeToUtf8Bytes(trimmed)));
    }

    [Theory]
    [InlineData("providerId")]
    [InlineData("modelId")]
    [InlineData("displayName")]
    public void Parse_WhenARequiredModelMemberIsBlank_ThrowsInvalidOperationException(string member)
    {
        var json = WithModel($$$"""{"providerId":"openai","modelId":"m","displayName":"M","status":"available","{{{member}}}":"  "}""");

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(json));
    }

    [Fact]
    public void Parse_WhenALimitIsAString_ThrowsJsonException()
    {
        var json = WithModel(/*lang=json,strict*/ """{"providerId":"openai","modelId":"m","displayName":"M","status":"available","maxContextTokens":"128000"}""");

        _ = Should.Throw<JsonException>(() => KnownModelCatalog.Parse(json));
    }

    [Fact]
    public void Parse_WhenALimitIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var json = WithModel(/*lang=json,strict*/ """{"providerId":"openai","modelId":"m","displayName":"M","status":"available","maxContextTokens":-1}""");

        _ = Should.Throw<ArgumentOutOfRangeException>(() => KnownModelCatalog.Parse(json));
    }

    [Fact]
    public void Parse_WhenPricingCurrencyIsMissing_ThrowsInvalidOperationException()
    {
        var json = WithModel(/*lang=json,strict*/ """{"providerId":"openai","modelId":"m","displayName":"M","status":"available","pricing":{"inputPerMillionTokens":1}}""");

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(json));
    }

    [Fact]
    public void Parse_WhenEntryIsMinimal_DefaultsOptionalFacts()
    {
        var json = WithModel(/*lang=json,strict*/ """{"providerId":"openai","modelId":"m","displayName":"M","status":"preview","replacedBy":""}""");

        var catalog = KnownModelCatalog.Parse(json);

        var model = catalog.Models.ShouldHaveSingleItem();
        model.Status.ShouldBe(KnownModelStatus.Preview);
        model.SupportsReasoning.ShouldBeFalse();
        model.SupportsVisionInput.ShouldBeFalse();
        model.SupportsToolCalls.ShouldBeFalse();
        model.Limits.MaxContextTokens.ShouldBeNull();
        model.Limits.MaxOutputTokens.ShouldBeNull();
        model.Pricing.ShouldBeNull();
        model.ReplacedBy.ShouldBeNull();
        catalog.Provenance.SourceCommit.ShouldBeNull();
        catalog.Provenance.GeneratedAt.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        catalog.Provenance.ImportedAt.ShouldBe(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Parse_WhenEntryCarriesUnknownMembers_IgnoresThem()
    {
        // The import script may add facts before the reader learns to use them; a newer resource must not break an older reader.
        var json = WithModel(/*lang=json,strict*/ """{"providerId":"openai","modelId":"m","displayName":"M","status":"available","futureFact":{"nested":[1,2,3]}}""");

        KnownModelCatalog.Parse(json).Models.ShouldHaveSingleItem().ModelId.ShouldBe(new ModelId("m"));
    }

    [Fact]
    public void Parse_WhenEmbeddedResourceIsParsedAgain_EqualsDefault()
    {
        using var stream = typeof(KnownModelCatalog).Assembly.GetManifestResourceStream("AgentKit.Providers.Resources.known-models.json").ShouldNotBeNull();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var reparsed = KnownModelCatalog.Parse(buffer.ToArray());

        reparsed.Provenance.ShouldBe(KnownModelCatalog.Default.Provenance);
        reparsed.Models.ShouldBe(KnownModelCatalog.Default.Models);
    }

    private const string Valid = /*lang=json,strict*/ """{"schemaVersion":1,"source":{"name":"x","url":"https://example.test/c.json","commit":null,"generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-02T00:00:00Z"},"models":[]}""";

    private static byte[] WithModel(string model) => Encoding.UTF8.GetBytes(Valid.Replace("\"models\":[]", $"\"models\":[{model}]", StringComparison.Ordinal));

    [Fact]
    public void Parse_WhenSourceUrlIsRelative_ThrowsInvalidOperationException()
    {
        var json = /*lang=json,strict*/ """{"schemaVersion":1,"source":{"name":"x","url":"c.json","generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-01T00:00:00Z"},"models":[]}""";

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(Encoding.UTF8.GetBytes(json)));
    }
}
