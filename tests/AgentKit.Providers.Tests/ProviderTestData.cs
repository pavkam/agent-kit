// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit;

/// <summary>
/// Deterministic builders for provider catalog and selection test values.
/// </summary>
internal static class ProviderTestData
{
    public static AgentId AgentId { get; } =
        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static SessionId SessionId { get; } =
        new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));

    public static OperationId OperationId { get; } =
        new(Guid.Parse("c0000000-0000-0000-0000-000000000003"));

    public static ModelRequestId ModelRequestId { get; } =
        new(Guid.Parse("d0000000-0000-0000-0000-000000000004"));

    /// <summary>
    /// Builds a descriptor whose capabilities default to "supports nothing
    /// optional", so a test opts in only to what it is actually exercising.
    /// </summary>
    public static ModelDescriptor Model(
        string alias,
        bool systemInstructions = true,
        bool streaming = false,
        bool toolCalls = false,
        bool parallelToolCalls = false,
        bool structuredOutput = false,
        bool reasoning = false,
        bool visionInput = false,
        long? maxContextTokens = null) =>
        new(
            new ModelAlias(alias),
            new ProviderId("test-provider"),
            new ApiFamilyId("test-family"),
            new ModelId($"{alias}-model"),
            null,
            new ModelCapabilities(
                systemInstructions,
                streaming,
                toolCalls,
                parallelToolCalls,
                structuredOutput,
                reasoning,
                visionInput,
                ExtensionData.Empty),
            new ModelLimits(maxContextTokens, null),
            null,
            ExtensionData.Empty);

    /// <summary>
    /// Builds an embedding descriptor whose capabilities default to
    /// "supports nothing optional", so a test opts in only to what it is
    /// actually exercising.
    /// </summary>
    public static EmbeddingModelDescriptor EmbeddingModel(
        string alias,
        bool batchInput = false,
        bool dimensions = false,
        bool purpose = false,
        bool encodingSelection = false,
        bool truncationControl = false) =>
        new(
            new EmbeddingModelAlias(alias),
            new ProviderId("test-provider"),
            new ApiFamilyId("test-family"),
            new ModelId($"{alias}-model"),
            null,
            new EmbeddingCapabilities(
                batchInput,
                dimensions,
                purpose,
                encodingSelection,
                truncationControl,
                ExtensionData.Empty),
            new EmbeddingLimits(null, null, null, null),
            null,
            ExtensionData.Empty);

    public static SecurityAuthorizationScope Scope() =>
        new(AgentId, SessionId, new BeforeRunOperationCorrelation(OperationId, null));

    public static ModelCatalogSnapshot Catalog(params ModelDescriptor[] models) =>
        new(new ModelCatalogVersion(1), [.. models]);

    public static ModelSelectionRequest SelectionRequest(
        ModelCatalogSnapshot catalog,
        ModelSelectionPolicy policy,
        ModelRequirements? requirements = null) =>
        new(
            Scope(),
            ModelRequestId,
            policy,
            requirements ?? ModelRequirements.None,
            catalog);

    public static ModelSelectionPolicy Policy(
        ModelFallbackPolicy fallback = ModelFallbackPolicy.FirstCandidateOnly,
        CapabilityDowngradePolicy downgrade = CapabilityDowngradePolicy.Reject,
        params string[] candidates) =>
        new(
            [.. candidates.Select(static alias => new ModelAlias(alias))],
            fallback,
            downgrade);
}
