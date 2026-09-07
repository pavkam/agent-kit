// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Configures syntax, complete-file, and final-plan bounds for patch execution.</summary>
public sealed class PatchToolOptions
{
    /// <summary>Gets or sets the maximum UTF-8 byte length accepted for patch syntax.</summary>
    /// <value>Defaults to 1 MiB and must be positive.</value>
    public int MaximumPatchBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the maximum entries accepted in one parsed patch.</summary>
    /// <value>Defaults to 100 and must be positive.</value>
    public int MaximumEntries { get; set; } = 100;

    /// <summary>Gets or sets the maximum complete source or final file byte count.</summary>
    /// <value>Defaults to 10 MiB and must be positive.</value>
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
}
