// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Enumerates bounded terminal outcomes of one durable execution event sink invocation.</summary>
internal enum DurableEventDispatchOutcome
{
    /// <summary>The sink accepted the event.</summary>
    Published,

    /// <summary>An observational sink was not registered in the container and was skipped.</summary>
    SinkSkipped,

    /// <summary>An observational sink threw and its failure was isolated.</summary>
    SinkFailed,

    /// <summary>A required sink was not resolvable, so publication failed closed.</summary>
    RequiredSinkUnavailable,

    /// <summary>A required sink threw, so publication failed closed.</summary>
    RequiredSinkFailed,

    /// <summary>Publication was cancelled before the sink completed.</summary>
    Cancelled,
}
