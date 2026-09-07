// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web;

/// <summary>Configures model-facing web-fetch bounds below the protected network boundary.</summary>
public sealed class WebFetchToolOptions
{
    /// <summary>Gets or sets the default maximum projected text characters.</summary>
    public int DefaultMaximumCharacters { get; set; } = 50_000;

    /// <summary>Gets or sets the absolute projected-text ceiling.</summary>
    public int MaximumCharacters { get; set; } = 200_000;

    /// <summary>Gets or sets the maximum compressed wire/body bytes admitted by the transport.</summary>
    public long MaximumResponseBytes { get; set; } = 2 * 1_024 * 1_024;

    /// <summary>Gets or sets the default overall fetch duration across every redirect hop.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Gets or sets the absolute caller-selectable overall timeout ceiling.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the per-hop connection timeout ceiling.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the maximum redirects followed with fresh authority.</summary>
    public int MaximumRedirects { get; set; } = 5;

    /// <summary>Gets or sets the maximum accepted absolute-URL characters.</summary>
    public int MaximumUrlCharacters { get; set; } = 4_096;

    /// <summary>Gets or sets the maximum response header count accepted for projection.</summary>
    public int MaximumHeaderCount { get; set; } = 100;

    /// <summary>Gets or sets the maximum aggregate response-header characters accepted for projection.</summary>
    public int MaximumHeaderCharacters { get; set; } = 32_768;
}
