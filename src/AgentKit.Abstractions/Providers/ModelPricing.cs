// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The published per-token pricing of one configured
/// <see cref="ModelDescriptor"/>, used for local cost estimation when a
/// provider response does not report an authoritative cost.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Pricing changes independently of capability and
/// limits, so it is optional on <see cref="ModelDescriptor"/> and never
/// required for a request to succeed.
/// </remarks>
public sealed record ModelPricing
{
    /// <summary>Initializes a new instance of the <see cref="ModelPricing"/> record.</summary>
    /// <param name="inputCostPerMillionTokens">The published cost per one million input tokens.</param>
    /// <param name="outputCostPerMillionTokens">The published cost per one million output tokens.</param>
    /// <param name="costCurrency">The ISO 4217 currency code the costs are denominated in.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="inputCostPerMillionTokens"/> or
    /// <paramref name="outputCostPerMillionTokens"/> is negative.
    /// </exception>
    public ModelPricing(
        decimal? inputCostPerMillionTokens,
        decimal? outputCostPerMillionTokens,
        string? costCurrency)
    {
        if (inputCostPerMillionTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputCostPerMillionTokens),
                inputCostPerMillionTokens,
                "Value must not be negative.");
        }

        if (outputCostPerMillionTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputCostPerMillionTokens),
                outputCostPerMillionTokens,
                "Value must not be negative.");
        }

        InputCostPerMillionTokens = inputCostPerMillionTokens;
        OutputCostPerMillionTokens = outputCostPerMillionTokens;
        CostCurrency = costCurrency;
    }

    /// <summary>Gets the published cost per one million input tokens.</summary>
    public decimal? InputCostPerMillionTokens { get; init; }

    /// <summary>Gets the published cost per one million output tokens.</summary>
    public decimal? OutputCostPerMillionTokens { get; init; }

    /// <summary>Gets the ISO 4217 currency code the costs are denominated in.</summary>
    public string? CostCurrency { get; init; }
}
