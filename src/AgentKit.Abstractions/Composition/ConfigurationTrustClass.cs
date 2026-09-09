// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a source according to its host-owned trust binding.</summary>
public enum ConfigurationTrustClass
{
    /// <summary>The host has not established source trust.</summary>
    Untrusted,
    /// <summary>The host independently established source trust.</summary>
    HostEstablished,
}
