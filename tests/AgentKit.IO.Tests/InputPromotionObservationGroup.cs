// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Serializes tests that install process-wide input-promotion diagnostic listeners.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InputPromotionObservationGroup
{
    /// <summary>Gets the stable xUnit collection name.</summary>
    public const string Name = "Input promotion observation";
}
