// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Configures barrier-segment scheduling, invocation bounds, and batch settlement defaults for the tool runtime.</summary>
/// <remarks>
/// Bound through <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> during composition. Host policy may
/// tighten untrusted tool hints; these options supply scheduler-wide limits and conservative handling for calls with
/// no declared scheduling compatibility.
/// </remarks>
public sealed class ToolRuntimeOptions
{
    /// <summary>Gets or sets the maximum raw argument bytes accepted per call during validation.</summary>
    public int MaximumArgumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum canonical result bytes retained after normalization before spill or truncation.</summary>
    public int MaximumResultBytes { get; set; } = 4_194_304;

    /// <summary>Gets or sets the maximum number of tool invocations that may run concurrently within one parallel segment.</summary>
    /// <value>Four by default, matching the architecture baseline for overlapping parallel-safe calls.</value>
    public int MaximumParallelInvocations { get; set; } = 4;

    /// <summary>Gets or sets the per-invocation attempt budget including the first attempt.</summary>
    public int MaximumRetryAttempts { get; set; } = 3;

    /// <summary>Gets or sets the default deadline applied to each tool batch unless overridden by capability evidence.</summary>
    public TimeSpan InvocationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets how sibling calls settle when one call in a batch fails.</summary>
    public ToolBatchFailureMode BatchFailureMode { get; set; } = ToolBatchFailureMode.SettleIndependently;

    /// <summary>Gets or sets how calls whose scheduling mode is <see cref="ToolSchedulingMode.Unspecified"/> are treated at schedule time.</summary>
    /// <value>
    /// <see cref="UnknownSchedulingMode.Sequential"/> by default, forming an ordering barrier rather than assuming
    /// parallel safety.
    /// </value>
    public UnknownSchedulingMode UnknownSchedulingMode { get; set; } = UnknownSchedulingMode.Sequential;
}
