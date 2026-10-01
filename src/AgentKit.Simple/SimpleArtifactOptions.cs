// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Configures the artifact coordinator that <c>WithArtifacts</c> publishes for every hosted agent.</summary>
/// <remarks>
/// The composition is deliberately narrow: one coordinator, one profile, one directory, and an ephemeral in-memory store. SQLite,
/// JSON, or file-system stores, several directories, retention, and event sinks are composed on
/// <see cref="AgentEngineBuilder.Services"/> with the artifact package's own registrations and a hand-written profile.
/// </remarks>
public sealed class SimpleArtifactOptions
{
    /// <summary>Gets or sets the largest artifact, in bytes, the coordinator accepts.</summary>
    /// <value>A positive byte count. The default is 16 MiB.</value>
    public long MaximumArtifactBytes { get; set; } = 16 * 1_024 * 1_024;

    /// <summary>Gets or sets the highest data classification process output artifacts are stored under.</summary>
    /// <value>A defined classification. The default is <see cref="DataClassification.Internal"/>.</value>
    public DataClassification ProcessOutputClassification { get; set; } = DataClassification.Internal;
}
