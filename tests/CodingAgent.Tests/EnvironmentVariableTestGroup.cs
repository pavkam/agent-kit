// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

/// <summary>Serializes tests that temporarily modify process environment variables.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableTestGroup
{
    /// <summary>Gets the stable xUnit collection name.</summary>
    public const string Name = "Environment variables";
}
