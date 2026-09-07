// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Serializes tests that observe process-wide diagnostic listener callbacks.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OutputObservabilityGroup
{
    /// <summary>Gets the stable xUnit collection name.</summary>
    public const string Name = "Output observability";
}
