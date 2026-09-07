// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language;

/// <summary>Configures default and absolute query bounds for model-facing language intelligence.</summary>
public sealed class LanguageToolOptions
{
    /// <summary>Gets or sets the default retained item count.</summary>
    public int DefaultMaximumResults { get; set; } = 100;
    /// <summary>Gets or sets the absolute retained item ceiling.</summary>
    public int MaximumResults { get; set; } = 1_000;
    /// <summary>Gets or sets the default total query duration.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Gets or sets the absolute total query-duration ceiling.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Gets or sets the maximum workspace-symbol query characters.</summary>
    public int MaximumQueryCharacters { get; set; } = 1_024;
    /// <summary>Gets or sets the maximum characters retained from any provider-returned text field.</summary>
    public int MaximumTextCharacters { get; set; } = 4_096;
}
