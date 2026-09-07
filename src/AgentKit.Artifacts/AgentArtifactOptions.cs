// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Configures bounded artifact mechanics and one captured logical profile.</summary>
public sealed class AgentArtifactOptions
{
    /// <summary>Gets or sets the largest complete artifact accepted.</summary>
    public long MaximumArtifactBytes { get; set; } = 16 * 1_024 * 1_024;
    /// <summary>Gets or sets the stream copy buffer.</summary>
    public int CopyBufferBytes { get; set; } = 64 * 1_024;
    /// <summary>Gets or sets the maximum unpublished staging lifetime.</summary>
    public TimeSpan PreparationLifetime { get; set; } = TimeSpan.FromHours(1);
    /// <summary>Gets or sets the logical profile key.</summary>
    public ArtifactProfileKey ProfileKey { get; set; } = new("default");
    /// <summary>Gets or sets the positive profile revision.</summary>
    public ArtifactProfileVersion ProfileVersion { get; set; } = new(1);
    /// <summary>Gets or sets the logical directory for complete process output.</summary>
    public ArtifactDirectoryId ProcessOutputDirectory { get; set; } = new("process-output");
    /// <summary>Gets or sets the retention policy recorded for complete process output.</summary>
    public ArtifactRetentionPolicyKey ProcessOutputRetentionPolicy { get; set; } = new("session");
    /// <summary>Gets or sets the data classification recorded for complete process output.</summary>
    public ArtifactDataClassification ProcessOutputClassification { get; set; } = ArtifactDataClassification.Internal;
}
