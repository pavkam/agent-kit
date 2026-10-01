// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Configures how one tool executor externalizes oversized results through its artifact coordinator.</summary>
/// <remarks>The values are validated and copied into an immutable snapshot when the spill is registered. They name no persistence target, path, credential, or authority: the selected coordinator's profile routes the directory and its store authorizes every effect.</remarks>
public sealed class ToolResultSpillOptions
{
    /// <summary>Gets or sets the logical artifact directory spilled results are stored in.</summary>
    /// <value>Required: a nonblank directory the coordinator's profile routes. There is no default because the directory belongs to the profile, not to the tool runtime.</value>
    public ArtifactDirectoryId Directory { get; set; }

    /// <summary>Gets or sets the retention policy recorded for spilled results.</summary>
    /// <value>A nonblank policy key. The default is <c>session</c>, so a spilled result is retained with the session that produced it.</value>
    public ArtifactRetentionPolicyKey RetentionPolicy { get; set; } = new("session");

    /// <summary>Gets or sets the data classification recorded for spilled results.</summary>
    /// <value>The default is <see cref="DataClassification.Internal"/>.</value>
    public DataClassification Classification { get; set; } = DataClassification.Internal;

    /// <summary>Gets or sets the media type recorded for spilled results.</summary>
    /// <value>A nonblank media type. The default is <c>text/plain; charset=utf-8</c>, because only complete text results are spilled.</value>
    public string MediaType { get; set; } = "text/plain; charset=utf-8";

    /// <summary>Gets or sets the longest the whole prepare, finalize, and abort sequence may take.</summary>
    /// <value>A positive duration. The default is thirty seconds; on expiry the result is truncated instead.</value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
