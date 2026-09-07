// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies one owned schema and bounded resources for local preflight.</summary>
public sealed record OutputSchemaPreflightRequest
{
    /// <summary>Initializes a preflight request.</summary>
    /// <param name="schema">The non-null owned schema to inspect.</param>
    /// <param name="limits">The non-null processing bounds.</param>
    /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
    public OutputSchemaPreflightRequest(JsonSchemaDocument schema, OutputSchemaProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(limits);

        Schema = schema;
        Limits = limits;
    }
    /// <summary>Gets the schema to preflight.</summary>
    public JsonSchemaDocument Schema { get; }
    /// <summary>Gets the applicable bounds.</summary>
    public OutputSchemaProcessingLimits Limits { get; }
}
