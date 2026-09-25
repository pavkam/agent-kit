// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Engine-wide defaults for provider-neutral model execution.</summary>
/// <remarks>
/// These options configure the first-party <see cref="IModelRequestExecutor"/> registration and bound attempt
/// deadlines. Same-model retry limits remain on each <see cref="ModelExecutionRequest.RetryPolicy"/>; this type does
/// not widen them at runtime.
/// </remarks>
public sealed class AgentProviderRuntimeOptions
{
    /// <summary>
    /// Gets or sets the default maximum number of attempts, including the first, suggested when a caller builds a
    /// <see cref="ProviderRetryPolicy"/>.
    /// </summary>
    /// <value>A positive count. The default is one attempt with no retry.</value>
    public int MaximumAttempts { get; set; } = 1;

    /// <summary>Gets or sets the per-attempt request deadline applied by the default executor.</summary>
    /// <value>A positive duration. The default is two minutes.</value>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the idle timeout reserved for streaming attempts.</summary>
    /// <value>A positive duration. The default is thirty seconds between stream events.</value>
    public TimeSpan StreamIdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether retryable failures may surface
    /// <see cref="ModelFallbackRequired"/> when <see cref="ModelExecutionRequest.Fallback"/> is
    /// <see cref="ModelFallbackPolicy.OrderedCandidates"/>.
    /// </summary>
    public bool AllowSemanticFallback { get; set; }
}
