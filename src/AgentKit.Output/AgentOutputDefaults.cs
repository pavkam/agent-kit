// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Exposes the explicit compatibility key for the first-party output-processing profile.</summary>
public static class AgentOutputDefaults
{
    /// <summary>Gets the key used by registration overloads that omit an output-processor profile.</summary>
    /// <value>The stable <c>agentkit-default-output</c> processor key.</value>
    public static ComponentKey<IOutputProcessor> ProcessorKey { get; } = new("agentkit-default-output");
}
