// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Text.Json;

/// <summary>Exposes explicitly retained schema evidence and a typed validation callback for contract-value tests.</summary>
public sealed class CallbackCompiledToolSchema: ICompiledToolSchema
{
    /// <inheritdoc/>
    public JsonSchema Schema { get; } = ToolSchemaTestData.Schema("{}");
    /// <inheritdoc/>
    public ToolSchemaProfile Profile { get; } = new(new("test-compiled-schema"), new(1), ToolSchemaTestData.Dialect, [], []);
    /// <inheritdoc/>
    public ToolSchemaLimits CompilationLimits { get; } = ToolSchemaTestData.Limits;
    /// <summary>Gets or sets a callback; missing behavior is never fabricated as successful validation.</summary>
    public Func<JsonElement, ToolSchemaLimits, CancellationToken, ToolSchemaValidationResult>? OnValidate { get; set; }
    /// <inheritdoc/>
    public ToolSchemaValidationResult Validate(JsonElement instance, ToolSchemaLimits limits, CancellationToken cancellationToken = default) =>
        OnValidate is { } callback ? callback(instance, limits, cancellationToken) : throw new InvalidOperationException("Validation was not configured.");
}
