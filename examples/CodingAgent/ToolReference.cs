// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Authors the model-facing tool reference shown by <c>/tools</c> and the Agent menu.</summary>
internal static class ToolReference
{
    /// <summary>The dialog title.</summary>
    public const string Title = "Available Tools";

    /// <summary>Builds the tool reference for the active permission mode.</summary>
    /// <param name="permissionMode">The permission mode applied to later protected effects.</param>
    /// <returns>Markdown describing every registered model-facing tool and its safety boundary.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="permissionMode"/> is unknown.</exception>
    internal static string BuildMarkdown(PermissionMode permissionMode)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(permissionMode), true, nameof(permissionMode));
        var permission = PermissionModeCatalog.Description(permissionMode);

        return $$"""
        # Available tools

        > The model can request these tools. AgentKit validates schemas and permissions before any invocation.

        ## Read and discover

        - **`read_file`** — Read a strict UTF-8 workspace file, optionally by line range.
        - **`glob`** — Match workspace paths with bounded AgentKit simple-glob traversal.
        - **`search`** — Search text with a literal pattern or bounded non-backtracking regular expression.

        ## Change and execute

        - **`write_file`** — Write with an explicit create, replace, create-or-replace, or append disposition.
        - **`edit`** — Replace exact text while preserving untouched bytes, newline spelling, and final-newline state.
        - **`command`** — Run one shell command inside the no-network workspace sandbox.

        ## Plan and ask

        - **`plan`** — Read or update the typed, revision-checked session work plan.
        - **`todo`** — Compatibility alias over the same typed plan state.
        - **`question`** — Ask one bounded multiple-choice question when a material choice cannot be inferred.

        ## Active safety policy

        **{{PermissionModeCatalog.Title(permissionMode)}}** — {{permission}} Commands run with the declared `PATH`, workspace confinement, process isolation, and network blocking. Optional toolchain roots are read-only.
        """;
    }
}
