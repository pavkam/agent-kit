// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the concrete resource named by a security request or grant.</summary>
public enum ProtectedResourceKind
{
    /// <summary>A canonical file path.</summary>
    File,
    /// <summary>A canonical directory path.</summary>
    Directory,
    /// <summary>A captured executable or process session.</summary>
    Process,
    /// <summary>A canonical network endpoint or origin.</summary>
    NetworkEndpoint,
    /// <summary>A typed application-state record.</summary>
    ApplicationState,
    /// <summary>A child goal or delegated attempt.</summary>
    Delegation,
    /// <summary>A logical durable artifact or unpublished preparation.</summary>
    Artifact,
}
