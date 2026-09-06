// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of an unexpected failure during a compaction attempt.</summary>
public enum CompactionFailureKind
{
    /// <summary>The source snapshot could not be loaded from the session store.</summary>
    SourceUnavailable,

    /// <summary>The compaction strategy failed to produce a checkpoint.</summary>
    StrategyFailure,

    /// <summary>The produced candidate failed validation for an unexpected reason.</summary>
    ValidationFailure,

    /// <summary>Appending the validated record to the session failed.</summary>
    ActivationFailure,

    /// <summary>An unclassified failure occurred.</summary>
    Unknown
}
