// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Configures one keyed operating-system process executor profile.</summary>
public sealed class AgentProcessOptions
{
    /// <summary>Gets the operating-system executable, workspace, and resource ceilings.</summary>
    public OperatingSystemProcessOptions OperatingSystem { get; } = new();

    /// <summary>Gets or sets the default workspace access when a start request does not override it.</summary>
    /// <value>Defaults to read-write workspace access.</value>
    public ProcessWorkspaceAccess DefaultWorkspaceAccess { get; set; } = ProcessWorkspaceAccess.ReadWrite;

    /// <summary>Gets or sets the default child-process policy when a start request does not override it.</summary>
    /// <value>Defaults to allowing sandboxed children.</value>
    public ProcessChildPolicy DefaultChildPolicy { get; set; } = ProcessChildPolicy.AllowSandboxed;
}
