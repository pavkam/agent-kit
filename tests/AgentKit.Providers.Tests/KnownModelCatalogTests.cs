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
    public void Parse_WhenSourceUrlIsRelative_ThrowsInvalidOperationException()
    {
        var json = /*lang=json,strict*/ """{"schemaVersion":1,"source":{"name":"x","url":"c.json","generatedAt":"2026-01-01T00:00:00Z","importedAt":"2026-01-01T00:00:00Z"},"models":[]}""";

        _ = Should.Throw<InvalidOperationException>(() => KnownModelCatalog.Parse(Encoding.UTF8.GetBytes(json)));
    }
}
