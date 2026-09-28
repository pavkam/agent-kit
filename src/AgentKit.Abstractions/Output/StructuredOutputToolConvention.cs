// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Reserved provider-visible identities for the internal synthetic structured-output tool channel.
/// </summary>
/// <remarks>
/// <para>
/// Synthetic output tools carry validated JSON through provider tool-call wire formats. They are not application
/// tools and must not appear in the permission-bearing tool catalog under unrelated aliases. The loop and context
/// assembler inject this declaration when an <see cref="OutputDefinition"/> uses
/// <see cref="OutputMode.SyntheticTool"/> or when a provider adapter maps native schema enforcement to forced-tool
/// semantics.
/// </para>
/// <para>
/// The reserved name is a framework convention documented here because the structured-output specification names
/// collision avoidance without prescribing a literal string in the normative contract block.
/// </para>
/// </remarks>
public static class StructuredOutputToolConvention
{
    /// <summary>Gets the stable tool identifier used for synthetic output declarations.</summary>
    public static ToolId ToolId { get; } = new("agentkit.structured_output");

    /// <summary>Gets the provider-visible function name for synthetic output tool calls.</summary>
    public static string ToolName { get; } = "agentkit_structured_output";

    /// <summary>Gets the tool alias reference used when correlating synthetic output calls in history.</summary>
    public static ToolReference ToolReference { get; } = new(new ToolAlias(ToolName), null, null);

    /// <summary>Determines whether a tool call part belongs to the synthetic output channel.</summary>
    /// <param name="call">The assistant tool call to inspect.</param>
    /// <returns><see langword="true"/> when the call targets the reserved synthetic output tool; otherwise, <see langword="false"/>.</returns>
    public static bool IsSyntheticOutputCall(ToolCallPart call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Tool.Id == ToolId
            || string.Equals(call.Tool.ProviderAlias.Value, ToolName, StringComparison.Ordinal);
    }

    /// <summary>Builds one model-facing tool definition carrying the output schema as its input schema.</summary>
    /// <param name="definition">The output contract whose schema is exposed to the provider.</param>
    /// <returns>A tool definition bound to the reserved synthetic output identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The definition has no schema to publish.</exception>
    public static LlmToolDefinition CreateToolDefinition(OutputDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Schema is not { } schema
            ? throw new InvalidOperationException("Synthetic structured output requires a schema on the output definition.")
            : new LlmToolDefinition(
                ToolId,
                ToolName,
                "Internal AgentKit channel for structured model output.",
                schema.Schema);
    }
}
