// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Configures deterministic executable mappings, operation scenarios, and the synthetic workspace root.</summary>
public sealed class ScriptedProcessOptions
{
    /// <summary>Gets or sets the absolute synthetic workspace root used in resolved evidence.</summary>
    public string WorkspaceRoot { get; set; } = "/scripted/workspace";
    /// <summary>Gets the exact executable mappings accepted by the resolver.</summary>
    public List<ScriptedExecutable> Executables { get; } = [];
    /// <summary>Gets the operation scenarios returned by the runner.</summary>
    public List<ScriptedProcessScenario> Scenarios { get; } = [];
    /// <summary>Gets the non-secret environment names admitted into deterministic requests.</summary>
    public List<string> AllowedEnvironmentVariableNames { get; } = [];
}
