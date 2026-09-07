// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Configures one root-jailed, sandbox-required operating-system process boundary.</summary>
public sealed class OperatingSystemProcessOptions
{
    /// <summary>Gets or sets the absolute workspace root exposed according to each request.</summary>
    /// <value>Must name an existing absolute directory.</value>
    public string RootDirectory { get; set; } = "";

    /// <summary>Gets the exact executable paths the resolver may admit.</summary>
    /// <value>Each entry must resolve to an existing executable; the list is empty by default.</value>
    public List<string> AllowedExecutablePaths { get; } = [];

    /// <summary>Gets the non-secret environment names requests may explicitly project.</summary>
    /// <value>No names are allowed by default, and ambient environment is always cleared.</value>
    public List<string> AllowedEnvironmentVariableNames { get; } = [];

    /// <summary>Gets or sets the maximum structured argument count.</summary>
    /// <value>Defaults to 256 and must be positive.</value>
    public int MaximumArgumentCount { get; set; } = 256;

    /// <summary>Gets or sets the maximum aggregate strict UTF-8 argument bytes.</summary>
    /// <value>Defaults to 1 MiB and must be positive.</value>
    public long MaximumArgumentBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the maximum standard-input bytes.</summary>
    /// <value>Defaults to 1 MiB and must be non-negative.</value>
    public long MaximumInputBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the maximum aggregate strict UTF-8 bytes in the explicit environment projection.</summary>
    /// <value>Defaults to 64 KiB and must be non-negative.</value>
    public long MaximumEnvironmentBytes { get; set; } = 64 * 1024;

    /// <summary>Gets or sets the maximum requested process timeout.</summary>
    /// <value>Defaults to ten minutes and must be positive.</value>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the maximum retained bytes accepted for each output stream.</summary>
    /// <value>Defaults to 10 MiB and must be positive and no greater than <see cref="int.MaxValue"/>.</value>
    public long MaximumOutputBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the largest complete output stream eligible for artifact preservation.</summary>
    /// <value>Defaults to 64 MiB and must be positive and no greater than <see cref="int.MaxValue"/>.</value>
    public long MaximumArtifactOutputBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Gets or sets the maximum concurrent owned processes.</summary>
    /// <value>Defaults to eight and must be positive.</value>
    public int MaximumConcurrentProcesses { get; set; } = 8;

    /// <summary>Gets or sets the bounded wait for process reaping after forced tree termination.</summary>
    /// <value>Defaults to two seconds and must be positive.</value>
    public TimeSpan ForcedTerminationWait { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the maximum executable bytes hashed during each verification.</summary>
    /// <value>Defaults to 512 MiB and must be positive.</value>
    public long MaximumExecutableBytes { get; set; } = 512L * 1024 * 1024;
}
