// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

/// <summary>Serializes tests that install process-wide activity listeners and ambient-current callbacks.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ActivityScopeObservationGroup
{
    /// <summary>Gets the stable xUnit collection name.</summary>
    public const string Name = "AgentKit activity scope observation";
}
