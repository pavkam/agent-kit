// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the eight configuration layers available to a captured precedence order.</summary>
public enum ConfigurationLayerKind
{
    /// <summary>Framework-provided defaults.</summary>
    LibraryDefaults,
    /// <summary>Host-wide configuration.</summary>
    HostGlobal,
    /// <summary>Host-established organization policy.</summary>
    TrustedOrganizationPolicy,
    /// <summary>Application or external-resource configuration.</summary>
    ApplicationOrExternalResource,
    /// <summary>One agent definition's configuration.</summary>
    AgentDefinition,
    /// <summary>Configuration contributed by composed capabilities.</summary>
    ComposedCapabilities,
    /// <summary>One run invocation's configuration.</summary>
    RunInvocation,
    /// <summary>One named next-turn override.</summary>
    NamedNextTurnOverride,
}
