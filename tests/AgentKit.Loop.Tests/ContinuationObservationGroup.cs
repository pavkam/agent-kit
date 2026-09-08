// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Serializes continuation tests that install process-wide diagnostic listeners.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ContinuationObservationGroup
{
    /// <summary>Gets the stable xUnit collection name.</summary>
    public const string Name = "Continuation observation";
}
