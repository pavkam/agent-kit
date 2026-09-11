// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Supplies an explicitly configured schema compiler for composition and malformed-collaborator tests.</summary>
public sealed class CallbackToolSchemaEngine: IToolSchemaEngine
{
    /// <inheritdoc/>
    public ToolSchemaProfile Profile { get; } = new(new("test-schema-engine"), new(1), ToolSchemaTestData.Dialect, [], []);
    /// <summary>Gets or sets the compiler callback; absence rejects vocabulary without fabricating a handle.</summary>
    public Func<JsonSchema, ToolSchemaLimits, CancellationToken, ToolSchemaCompilationResult>? OnCompile { get; set; }
    /// <inheritdoc/>
    public ToolSchemaCompilationResult Compile(JsonSchema schema, ToolSchemaLimits limits, CancellationToken cancellationToken = default) =>
        OnCompile is { } callback ? callback(schema, limits, cancellationToken) : new ToolSchemaCompilationRejected(ToolSchemaRejectionReason.UnsupportedKeyword);
}
