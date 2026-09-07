// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command;

/// <summary>Configures the explicit shell identity and bounded model-facing command profile.</summary>
public sealed class CommandToolOptions
{
    /// <summary>Gets or sets the exact configured shell executable passed to the process resolver.</summary>
    /// <value>Must be a non-blank configured process executable; no ambient shell is inferred.</value>
    public string ShellExecutable { get; set; } = "/bin/sh";

    /// <summary>Gets the fixed arguments inserted before the untrusted command string.</summary>
    /// <value>Defaults to <c>-lc</c>; the command is always one final structured argument.</value>
    public List<string> ShellArguments { get; } = ["-lc"];

    /// <summary>Gets or sets the required process sandbox profile.</summary>
    public SandboxProfileId SandboxProfile { get; set; } = new("workspace-no-network-v1");

    /// <summary>Gets or sets the default operation timeout.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum operation timeout a model may request.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the default retained bytes independently applied to stdout and stderr.</summary>
    public long DefaultMaximumOutputBytes { get; set; } = 50 * 1024;

    /// <summary>Gets or sets the maximum retained bytes per output stream a model may request.</summary>
    public long MaximumOutputBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the graceful termination window before forced process-tree teardown.</summary>
    public TimeSpan TerminationGracePeriod { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the maximum strict UTF-8 bytes accepted in one command string.</summary>
    public long MaximumCommandBytes { get; set; } = 1024 * 1024;
}
