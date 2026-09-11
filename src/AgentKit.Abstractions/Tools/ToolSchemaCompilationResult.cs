// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents complete compilation or a classified rejection with no partial executable schema.</summary>
public abstract record ToolSchemaCompilationResult
{
    /// <summary>Restricts compilation outcomes to the declared success and rejection cases.</summary>
    /// <remarks>Implementations return a complete immutable handle or an explicit rejection.</remarks>
    private protected ToolSchemaCompilationResult() { }
}
