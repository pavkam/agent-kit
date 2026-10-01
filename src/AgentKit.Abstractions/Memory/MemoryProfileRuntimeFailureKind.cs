// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why a memory profile runtime could not be activated.</summary>
public enum MemoryProfileRuntimeFailureKind
{
    /// <summary>No profile with the requested key is registered.</summary>
    UnknownProfile = 0,

    /// <summary>The profile exists but not at the requested version.</summary>
    VersionMismatch = 1,

    /// <summary>A collaborator the profile names is not registered or cannot be activated.</summary>
    MissingCapability = 2,

    /// <summary>The profile's collaborators cannot be combined into a valid runtime.</summary>
    InvalidComposition = 3,
}
