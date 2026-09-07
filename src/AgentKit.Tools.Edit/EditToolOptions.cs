// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit;

/// <summary>Configures complete-file byte bounds for exact text replacement.</summary>
public sealed class EditToolOptions
{
    /// <summary>Gets or sets the default maximum complete input and final byte count.</summary>
    public long DefaultMaximumBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the host-configured byte ceiling accepted from callers.</summary>
    public long MaximumBytes { get; set; } = 10 * 1024 * 1024;
}
