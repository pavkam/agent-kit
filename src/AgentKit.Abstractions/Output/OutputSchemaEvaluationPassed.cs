// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Singleton result indicating local schema evaluation passed.</summary>
public sealed record OutputSchemaEvaluationPassed: OutputSchemaEvaluationResult
{
    /// <summary>Gets the shared pass result.</summary>
    public static OutputSchemaEvaluationPassed Instance { get; } = new();

    private OutputSchemaEvaluationPassed()
    {
    }
}
