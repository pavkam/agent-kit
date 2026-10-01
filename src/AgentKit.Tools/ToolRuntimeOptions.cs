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

    /// <summary>Gets or sets the per-call attempt budget the default execution policy plans, including the first attempt.</summary>
    /// <value>Three by default: one initial attempt and at most two retries. One disables retry.</value>
    public int MaximumAttempts { get; set; } = 3;

    /// <summary>Gets or sets the delay before the second attempt the default execution policy plans, before jitter.</summary>
    /// <value>200 milliseconds by default.</value>
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets the multiplier applied to the retry delay for each later attempt.</summary>
    /// <value>Two by default; must be at least one.</value>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>Gets or sets the upper bound for any retry delay.</summary>
    /// <value>Five seconds by default; must not be less than <see cref="RetryInitialDelay"/>.</value>
    public TimeSpan RetryMaximumDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the fraction of each retry delay that injectable jitter may remove.</summary>
    /// <value>0.2 by default; zero is fully deterministic and never consults the randomizer.</value>
    public double RetryJitterFraction { get; set; } = 0.2;

    /// <summary>Gets or sets how many times the session recorder re-reads the branch tip and appends after an optimistic-concurrency conflict.</summary>
    /// <value>Four by default; each conflict means another writer advanced the session between the read and the append.</value>
    public int MaximumRecordAppendAttempts { get; set; } = 4;

    /// <summary>Gets or sets how many of the most recent branch entries the session recorder searches for a call's accepted record before recording its terminal outcome.</summary>
    /// <value>64 by default. The accepted record is appended immediately before invocation, so it sits within the entries a batch of calls produced.</value>
    public int AcceptedRecordLookupEntries { get; set; } = 64;

    /// <summary>Gets or sets the longest a single event sink may take to observe one event before the dispatcher abandons that delivery.</summary>
    /// <value>Five seconds by default. A timed-out sink is counted as failed and never changes an outcome.</value>
    public TimeSpan EventSinkTimeout { get; set; } = TimeSpan.FromSeconds(5);

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

    /// <summary>
    /// Gets or sets the bounds used to compile each tool's declared input schema and to validate call arguments.
    /// </summary>
    /// <value>
    /// 256 KiB of raw UTF-8 JSON, a maximum depth of 64, at most 10,000 total JSON values, and a work budget of
    /// 100,000 deterministic units by default.
    /// </value>
    public ToolSchemaLimits ArgumentValidationLimits { get; set; } = new(
        maximumUtf8Bytes: 262_144, maximumDepth: 64, maximumNodes: 10_000, maximumWork: 100_000);
}
