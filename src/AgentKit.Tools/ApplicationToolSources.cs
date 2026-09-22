// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Stable source identities for application-local tools registered through <see cref="ServiceExtensions.AddToolInvoker{TInvoker}"/>.</summary>
public static class ApplicationToolSources
{
    /// <summary>Gets the shared application tool source used by <see cref="ApplicationToolProvider"/>.</summary>
    /// <value>
    /// Toolset publications that include application-registered invokers must select this exact
    /// <see cref="ToolSourceId"/> in their authored source membership.
    /// </value>
    public static ToolSourceId Default { get; } = new("agentkit.tools.application");
}
