// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

/// <summary>Discovery bounds for workspace project instruction files.</summary>
public sealed class ProjectInstructionOptions
{
    /// <summary>Gets or sets relative search roots scanned for instruction filenames.</summary>
    /// <value>Nonblank relative paths; default scans the workspace root only.</value>
    public string[] SearchRoots { get; set; } = ["."];

    /// <summary>Gets or sets instruction filenames discovered under each search root.</summary>
    /// <value>Nonblank filenames compared case-sensitively against the host file-system boundary.</value>
    public string[] InstructionFilenames { get; set; } = ["AGENTS.md", "CLAUDE.md"];

    /// <summary>Gets or sets the maximum bytes read from one discovered instruction file.</summary>
    public int MaxBytesPerFile { get; set; } = 256_000;
}
