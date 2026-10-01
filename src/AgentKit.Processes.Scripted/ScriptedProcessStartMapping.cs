// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Maps structured start requests onto the resolve requests the scripted resolver consumes.</summary>
internal static class ScriptedProcessStartMapping
{
    /// <summary>Builds the resolve request for the scripted resolver.</summary>
    /// <param name="start">The structured start request.</param>
    /// <param name="snapshot">The captured scripted profile.</param>
    /// <returns>The resolve request.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static ProcessResolveRequest ToResolveRequest(ProcessStartRequest start, ScriptedProcessOptionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(snapshot);
        var mapped = ProcessStartBinding.ToResolveRequest(start);
        return new ProcessResolveRequest(
            mapped.Id,
            mapped.Executable,
            mapped.Arguments,
            mapped.WorkingDirectory,
            mapped.Environment,
            mapped.StandardInput,
            mapped.SandboxProfile,
            mapped.WorkspaceAccess,
            mapped.SideEffectClass,
            ProcessChildPolicy.Deny,
            mapped.Limits);
    }
}
