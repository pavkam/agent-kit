// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Text.Json;

/// <summary>Creates owned canonical schema and generous deterministic bounds for typed schema-engine fixtures.</summary>
public static class ToolSchemaTestData
{
    /// <summary>Gets bounds large enough for ordinary contract examples while remaining explicit.</summary>
    public static ToolSchemaLimits Limits { get; } = new(65_536, 64, 16_384, 16_000_000);
    /// <summary>Gets the canonical first-party tool dialect.</summary>
    public static JsonSchemaDialectId Dialect { get; } = new("https://json-schema.org/draft/2020-12/schema");
    /// <summary>Parses an owned canonical schema without retaining a disposable document.</summary>
    /// <param name="json">Complete schema JSON.</param>
    /// <returns>An owned schema declaring the test dialect.</returns>
    public static JsonSchema Schema(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        return new(Dialect, document.RootElement);
    }
    /// <summary>Parses a complete owned instance for deterministic validation.</summary>
    /// <param name="json">Complete instance JSON.</param>
    /// <returns>A cloned JSON element independent of the parser lifetime.</returns>
    public static JsonElement Instance(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        return document.RootElement.Clone();
    }
}
