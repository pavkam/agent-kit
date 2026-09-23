// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Maps legacy resolved intents onto spec sandbox construction requests.</summary>
internal static class ProcessSandboxRequestMapping
{
    /// <summary>Builds a sandbox request from one legacy resolved intent.</summary>
    /// <param name="intent">The legacy resolved intent.</param>
    /// <returns>The equivalent sandbox construction request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    internal static ProcessSandboxRequest FromResolvedProcessIntent(ResolvedProcessIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new ProcessSandboxRequest(
            intent.Request.Id,
            intent.Request.SandboxProfile,
            new ResolvedExecutable(intent.AbsoluteExecutablePath, intent.ExecutableFingerprint),
            intent.AbsoluteWorkspaceRoot,
            intent.AbsoluteWorkingDirectory,
            intent.Request.WorkspaceAccess,
            intent.Request.ChildPolicy,
            intent.Request.Environment,
            intent.Request.Arguments,
            intent.Request.ReadOnlyRoots);
    }
}
