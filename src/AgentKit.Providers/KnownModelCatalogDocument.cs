// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Text.Json.Serialization;

/// <summary>The wire shape of the <c>known-models.json</c> resource; deserialized then converted into <see cref="KnownModelCatalog"/>.</summary>
internal sealed class KnownModelCatalogDocument
{
    /// <summary>Gets or sets the document schema version; only version 1 is supported.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>Gets or sets where the data came from.</summary>
    [JsonPropertyName("source")]
    public SourceDocument? Source { get; set; }

    /// <summary>Gets or sets the models, unique by provider and model identifier.</summary>
    [JsonPropertyName("models")]
    public List<ModelDocument>? Models { get; set; }

    /// <summary>The wire shape of the provenance block.</summary>
    internal sealed class SourceDocument
    {
        /// <summary>Gets or sets a short name for the upstream feed.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the URL the feed was fetched from.</summary>
        [JsonPropertyName("url")]
        public Uri? Url { get; set; }

        /// <summary>Gets or sets the upstream revision, when published.</summary>
        [JsonPropertyName("commit")]
        public string? Commit { get; set; }

        /// <summary>Gets or sets when the upstream feed was generated.</summary>
        [JsonPropertyName("generatedAt")]
        public DateTimeOffset GeneratedAt { get; set; }

        /// <summary>Gets or sets when the feed was converted into this resource.</summary>
        [JsonPropertyName("importedAt")]
        public DateTimeOffset ImportedAt { get; set; }
    }

    /// <summary>The wire shape of one model entry.</summary>
    internal sealed class ModelDocument
    {
        /// <summary>Gets or sets the AgentKit provider identifier.</summary>
        [JsonPropertyName("providerId")]
        public string? ProviderId { get; set; }

        /// <summary>Gets or sets the provider's own model identifier.</summary>
        [JsonPropertyName("modelId")]
        public string? ModelId { get; set; }

        /// <summary>Gets or sets the human-readable name.</summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>Gets or sets the vendor-reported availability.</summary>
        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter<KnownModelStatus>))]
        public KnownModelStatus Status { get; set; }

        /// <summary>Gets or sets whether the model exposes reasoning.</summary>
        [JsonPropertyName("supportsReasoning")]
        public bool SupportsReasoning { get; set; }

        /// <summary>Gets or sets whether the model accepts image input.</summary>
        [JsonPropertyName("supportsVisionInput")]
        public bool SupportsVisionInput { get; set; }

        /// <summary>Gets or sets whether the model can request tool calls.</summary>
        [JsonPropertyName("supportsToolCalls")]
        public bool SupportsToolCalls { get; set; }

        /// <summary>Gets or sets the published context window, when known.</summary>
        [JsonPropertyName("maxContextTokens")]
        public long? MaxContextTokens { get; set; }

        /// <summary>Gets or sets the published output limit, when known.</summary>
        [JsonPropertyName("maxOutputTokens")]
        public long? MaxOutputTokens { get; set; }

        /// <summary>Gets or sets the published list prices, when any.</summary>
        [JsonPropertyName("pricing")]
        public PricingDocument? Pricing { get; set; }

        /// <summary>Gets or sets the successor a deprecated model points to, when any.</summary>
        [JsonPropertyName("replacedBy")]
        public string? ReplacedBy { get; set; }
    }

    /// <summary>The wire shape of one pricing block.</summary>
    internal sealed class PricingDocument
    {
        /// <summary>Gets or sets the ISO 4217 currency code.</summary>
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        /// <summary>Gets or sets the uncached-input list price per million tokens.</summary>
        [JsonPropertyName("inputPerMillionTokens")]
        public decimal? InputPerMillionTokens { get; set; }

        /// <summary>Gets or sets the output list price per million tokens.</summary>
        [JsonPropertyName("outputPerMillionTokens")]
        public decimal? OutputPerMillionTokens { get; set; }

        /// <summary>Gets or sets the cache-read list price per million tokens.</summary>
        [JsonPropertyName("cacheReadPerMillionTokens")]
        public decimal? CacheReadPerMillionTokens { get; set; }

        /// <summary>Gets or sets the cache-write list price per million tokens.</summary>
        [JsonPropertyName("cacheWritePerMillionTokens")]
        public decimal? CacheWritePerMillionTokens { get; set; }
    }
}
