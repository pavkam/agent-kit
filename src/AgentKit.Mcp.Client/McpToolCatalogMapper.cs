// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Text.Json.Nodes;

using ModelContextProtocol.Protocol;

/// <summary>Maps remote MCP tool metadata into AgentKit catalog descriptors.</summary>
internal static class McpToolCatalogMapper
{
    private static readonly JsonSchemaDialectId _schemaDialect = new("https://json-schema.org/draft/2020-12/schema");

    internal static ToolDescriptor ToDescriptor(Tool tool, ToolSourceId sourceId, McpToolName protocolName)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var inputSchema = tool.InputSchema is { } input
            ? new JsonSchema(_schemaDialect, input)
            : new JsonSchema(_schemaDialect, JsonDocument.Parse("{}").RootElement);
        var version = ReadToolVersion(tool.Meta) ?? new ToolVersion("0");
        var toolId = new ToolId($"mcp.{sourceId.Value}.{protocolName.Value}");
        return new ToolDescriptor(
            toolId,
            version,
            protocolName.Value,
            string.IsNullOrWhiteSpace(tool.Description) ? protocolName.Value : tool.Description,
            inputSchema,
            outputSchema: null,
            new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
            sourceId,
            ExtensionData.Empty);
    }

    internal static McpResourceDescriptor ToResource(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new McpResourceDescriptor(
            resource.Uri ?? resource.Name ?? string.Empty,
            resource.Name,
            resource.Description,
            resource.MimeType);
    }

    internal static McpPromptDescriptor ToPrompt(Prompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new McpPromptDescriptor(
            prompt.Name,
            prompt.Description,
            prompt.Arguments?.Select(static argument => argument.Name).ToImmutableArray() ?? []);
    }

    internal static McpCapabilitySet ToCapabilitySet(ServerCapabilities? capabilities) =>
        capabilities is null
            ? new McpCapabilitySet()
            : new McpCapabilitySet(
                tools: capabilities.Tools is not null,
                resources: capabilities.Resources is not null,
                prompts: capabilities.Prompts is not null,
                logging: false);

    private static ToolVersion? ReadToolVersion(JsonObject? metadata)
    {
        var node = metadata?[McpMetadataKeys.ToolContractVersion];
        return node is JsonValue value && value.TryGetValue<string>(out var version) && !string.IsNullOrWhiteSpace(version)
            ? new ToolVersion(version)
            : null;
    }
}
