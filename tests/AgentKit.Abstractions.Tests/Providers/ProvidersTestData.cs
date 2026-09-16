// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>
/// Deterministic builders for provider-selection contract values shared
/// across the Providers fixture files.
/// </summary>
internal static class ProvidersTestData
{
    public static ModelCapabilities Capabilities() =>
        new(supportsSystemInstructions: true, supportsStreaming: true, supportsToolCalls: true,
            supportsParallelToolCalls: false, supportsStructuredOutput: false, supportsReasoning: false,
            supportsVisionInput: false, ExtensionData.Empty);

    public static ModelLimits Limits() => new(maxContextTokens: 4096, maxOutputTokens: 1024);

    public static ModelDescriptor Descriptor(string alias = "chat") =>
        new(new ModelAlias(alias), new ProviderId("test-provider"), new ApiFamilyId("test-api"),
            new ModelId("test-model"), deploymentId: null, Capabilities(), Limits(), pricing: null, ExtensionData.Empty);

    public static ModelSelectionDiagnostic Diagnostic() =>
        new(new ModelAlias("skipped"), ModelCandidateOutcome.NotEvaluated, "not compatible");

    public static ModelSelectionDecision Decision() =>
        new(Descriptor(), new ModelCatalogVersion(1), "best match", [Diagnostic()]);
}
