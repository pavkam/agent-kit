// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Configures version defaults applied when registering a named toolset publication.</summary>
/// <remarks>
/// Bound through typed options at composition when hosts use <see cref="ServiceExtensions.AddToolset"/> overloads
/// that accept configuration. This type carries no runtime behavior on its own.
/// </remarks>
public sealed class ToolsetOptions
{
    /// <summary>Gets or sets the default toolset version assigned when a publication omits an explicit revision.</summary>
    public ToolsetVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the default execution-policy version referenced by toolset members when unspecified.</summary>
    public ToolExecutionPolicyVersion ExecutionPolicyVersion { get; set; } = new(1);
}
