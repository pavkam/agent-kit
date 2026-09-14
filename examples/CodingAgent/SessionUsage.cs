// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Globalization;

/// <summary>Accumulates the running token and cost totals a session's <c>ConversationUsageEvent</c>s report.</summary>
/// <remarks>Cost accumulates only while every reporting response agrees on one currency; a provider switching
/// currency mid-session stops further cost accumulation rather than silently summing incompatible units.</remarks>
internal sealed class SessionUsage
{
    private string? _currency;
    private bool _costMixedCurrency;

    /// <summary>Gets the cumulative reported input (prompt) tokens.</summary>
    public long InputTokens { get; private set; }

    /// <summary>Gets the cumulative reported output (completion) tokens.</summary>
    public long OutputTokens { get; private set; }

    /// <summary>Gets the cumulative estimated cost, or null when no response has reported one.</summary>
    public decimal? EstimatedCost { get; private set; }

    /// <summary>Gets whether any usage has been recorded yet.</summary>
    public bool HasData => InputTokens > 0 || OutputTokens > 0 || EstimatedCost is not null;

    /// <summary>Clears every running total, for a session restarted from scratch.</summary>
    public void Reset()
    {
        InputTokens = 0;
        OutputTokens = 0;
        EstimatedCost = null;
        _currency = null;
        _costMixedCurrency = false;
    }

    /// <summary>Folds one reported usage sample into the running totals.</summary>
    public void Add(ModelUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        InputTokens += usage.InputTokens ?? 0;
        OutputTokens += usage.OutputTokens ?? 0;
        if (usage.EstimatedCost is not { } cost || _costMixedCurrency)
        {
            return;
        }

        if (_currency is not null && usage.CostCurrency is not null && _currency != usage.CostCurrency)
        {
            _costMixedCurrency = true;
            EstimatedCost = null;
            return;
        }

        _currency ??= usage.CostCurrency;
        EstimatedCost = (EstimatedCost ?? 0m) + cost;
    }

    /// <summary>Renders a compact one-line summary, or null when nothing has been reported yet.</summary>
    public string? Format()
    {
        if (!HasData)
        {
            return null;
        }

        var tokens = FormatTokenCount(InputTokens + OutputTokens);
        return EstimatedCost is { } cost
            ? $"{tokens} tok · {FormatCost(cost)}"
            : $"{tokens} tok";
    }

    private string FormatCost(decimal cost) =>
        _currency switch
        {
            "USD" or null => $"${cost:0.00##}",
            _ => $"{cost:0.00##} {_currency}",
        };

    private static string FormatTokenCount(long tokens) => tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000.0:0.#}M",
        >= 1_000 => $"{tokens / 1_000.0:0.#}K",
        _ => tokens.ToString(CultureInfo.InvariantCulture),
    };
}
