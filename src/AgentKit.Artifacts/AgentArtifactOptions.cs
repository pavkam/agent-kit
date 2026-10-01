// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Configures the bounded mechanics and process-output policy of one keyed artifact coordinator.</summary>
/// <remarks>The values are validated and copied into an immutable snapshot when the coordinator is registered, so later mutation of this object never changes an authorized attempt. These are safe finite mechanics: they name no persistence target, path, credential, grant, or authority.</remarks>
public sealed class AgentArtifactOptions
{
    /// <summary>Gets or sets the largest complete artifact accepted, in bytes.</summary>
    /// <value>A positive bound. The default is sixteen mebibytes.</value>
    public long MaximumArtifactBytes { get; set; } = 16 * 1_024 * 1_024;

    /// <summary>Gets or sets the stream copy buffer, in bytes.</summary>
    /// <value>A positive size. The default is 64 KiB.</value>
    public int CopyBufferBytes { get; set; } = 64 * 1_024;

    /// <summary>Gets or sets the maximum unpublished staging lifetime.</summary>
    /// <value>A positive duration. The default is one hour.</value>
    public TimeSpan PreparationLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets how long a reference-commit intent is retained after it is recorded before reconciliation may fence and collect it.</summary>
    /// <value>A positive duration. The default is twenty-four hours, so conservative retention is the default and a time-based sweep alone never proves an orphan.</value>
    public TimeSpan OrphanRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets whether metadata must declare the complete-content hash.</summary>
    /// <value><see langword="true"/> by default, so every artifact is verified against a caller-declared integrity value.</value>
    public bool RequireDeclaredContentHash { get; set; } = true;

    /// <summary>Gets or sets the logical directory for complete process output, or <see langword="null"/> to use the profile's default directory.</summary>
    public ArtifactDirectoryId? ProcessOutputDirectory { get; set; }

    /// <summary>Gets or sets the retention policy recorded for complete process output.</summary>
    /// <value>A non-blank policy key. The default is <c>session</c>.</value>
    public ArtifactRetentionPolicyKey ProcessOutputRetentionPolicy { get; set; } = new("session");

    /// <summary>Gets or sets the data classification recorded for complete process output.</summary>
    /// <value>The default is <see cref="DataClassification.Internal"/>.</value>
    public DataClassification ProcessOutputClassification { get; set; } = DataClassification.Internal;
}
