// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

/// <summary>Represents an unrelated tool registration used to prove additive composition.</summary>
internal sealed class StubTool: ITool
{
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(/*lang=json,strict*/ """{"type":"object"}""").RootElement;

    public ToolDescriptor Descriptor { get; } = new(
        new ToolId("stub"),
        new ToolVersion("1.0"),
        "stub",
        "A no-op tool used by dependency-injection registration tests.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, null, null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
        new ToolSourceId("agentkit.tools.write.tests"), ExtensionData.Empty);

    public Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
            []));
}
