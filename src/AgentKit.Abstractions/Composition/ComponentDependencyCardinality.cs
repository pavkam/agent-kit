// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Specifies how a component consumes matching registrations for one dependency reference.</summary>
public enum ComponentDependencyCardinality
{
    /// <summary>Exactly one matching registration must be available.</summary>
    RequiredSingular = 0,

    /// <summary>Every matching registration is consumed; no matching registrations is valid.</summary>
    AdditiveCollection = 1,

    /// <summary>Zero or one matching registration may be available; multiple matches are ambiguous.</summary>
    /// <remarks>When one registration is present, it participates in cycle and lifetime validation exactly like a required singular dependency. Absence is valid and contributes no graph edge.</remarks>
    OptionalSingular = 2,
}
