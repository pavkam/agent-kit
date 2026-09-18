// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Per-run hard limits the builder turns into <see cref="BudgetLimit"/> values for the default agent.</summary>
/// <remarks>
/// Every limit is optional; unset members impose nothing. Turns, model requests, and tool calls are counted when the
/// loop commits to the attempt; tokens and cost are accounted from provider-reported usage after each response, so a
/// single response may cross a token or cost limit and the run then stops before the next request. Cost limits are
/// in USD and apply only to responses the provider or catalog prices in USD.
/// </remarks>
public sealed class SimpleBudgetOptions
{
    /// <summary>Gets or sets the maximum turns per run, or <see langword="null"/> for no budgeted limit beyond <c>WithMaxTurns</c>.</summary>
    public int? MaxTurns { get; set; }

    /// <summary>Gets or sets the maximum model requests per run.</summary>
    public int? MaxModelRequests { get; set; }

    /// <summary>Gets or sets the maximum attempted tool calls per run.</summary>
    public int? MaxToolCalls { get; set; }

    /// <summary>Gets or sets the maximum reported input tokens per run.</summary>
    public long? MaxInputTokens { get; set; }

    /// <summary>Gets or sets the maximum reported output tokens per run.</summary>
    public long? MaxOutputTokens { get; set; }

    /// <summary>Gets or sets the maximum reported cost per run, in USD.</summary>
    public decimal? MaxCostUsd { get; set; }
}
