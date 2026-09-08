// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>Represents an unrelated tool registration used to prove additive composition.</summary>
internal sealed class StubTool: ITool
{
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(/*lang=json,strict*/ """{"type":"object"}""").RootElement;

    public ToolDescriptor Descriptor { get; } = new(
        new ToolId("stub"),
        null,
        "stub",
        "A no-op tool used by dependency-injection registration tests.",
        _inputSchema,
        ToolEffect.ReadOnly, ExtensionData.Empty);

    public Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
            []));
}
