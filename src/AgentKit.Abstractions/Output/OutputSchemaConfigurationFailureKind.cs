// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a fail-closed schema configuration or preflight failure.</summary>
public enum OutputSchemaConfigurationFailureKind
{
    /// <summary>The schema is not well formed.</summary>
    MalformedSchema,
    /// <summary>The declared dialect is unavailable.</summary>
    UnsupportedDialect,
    /// <summary>The schema uses an assertion the profile cannot enforce.</summary>
    UnsupportedVocabulary,
    /// <summary>A required reference cannot be resolved.</summary>
    UnresolvedReference,
    /// <summary>Bounded schema processing exceeded a configured limit.</summary>
    ResourceLimitExceeded,
    /// <summary>Captured preflight evidence does not describe the evaluated schema.</summary>
    PreflightEvidenceMismatch,
}
