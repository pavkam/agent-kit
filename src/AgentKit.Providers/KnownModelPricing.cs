// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Published per-million-token list prices for a <see cref="KnownModel"/>, as reported by the vendor feed.</summary>
/// <remarks>
/// Every amount is optional because feeds publish different subsets. Prices are list prices at import
/// time and carry no tiering, discount, or committed-use information; a host that bills against them
/// owns their currency of the day and any tiered adjustment. Instances are immutable value objects.
/// </remarks>
public sealed record KnownModelPricing
{
    /// <summary>Initializes a new instance of the <see cref="KnownModelPricing"/> record.</summary>
    /// <param name="currency">The ISO 4217 currency code every amount is denominated in.</param>
    /// <param name="inputPerMillionTokens">The list price per one million uncached input tokens, when published.</param>
    /// <param name="outputPerMillionTokens">The list price per one million output tokens, when published.</param>
    /// <param name="cacheReadPerMillionTokens">The list price per one million cached-input tokens read, when published.</param>
    /// <param name="cacheWritePerMillionTokens">The list price per one million input tokens written to a prompt cache, when published.</param>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any supplied amount is negative.</exception>
    public KnownModelPricing(
        string currency,
        decimal? inputPerMillionTokens,
        decimal? outputPerMillionTokens,
        decimal? cacheReadPerMillionTokens,
        decimal? cacheWritePerMillionTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ThrowIfNegative(inputPerMillionTokens);
        ThrowIfNegative(outputPerMillionTokens);
        ThrowIfNegative(cacheReadPerMillionTokens);
        ThrowIfNegative(cacheWritePerMillionTokens);

        Currency = currency;
        InputPerMillionTokens = inputPerMillionTokens;
        OutputPerMillionTokens = outputPerMillionTokens;
        CacheReadPerMillionTokens = cacheReadPerMillionTokens;
        CacheWritePerMillionTokens = cacheWritePerMillionTokens;
    }

    /// <summary>Gets the ISO 4217 currency code every amount is denominated in.</summary>
    public string Currency { get; }

    /// <summary>Gets the list price per one million uncached input tokens, or <see langword="null"/> when unpublished.</summary>
    public decimal? InputPerMillionTokens { get; }

    /// <summary>Gets the list price per one million output tokens, or <see langword="null"/> when unpublished.</summary>
    public decimal? OutputPerMillionTokens { get; }

    /// <summary>Gets the list price per one million cached-input tokens read, or <see langword="null"/> when unpublished.</summary>
    public decimal? CacheReadPerMillionTokens { get; }

    /// <summary>Gets the list price per one million input tokens written to a prompt cache, or <see langword="null"/> when unpublished.</summary>
    public decimal? CacheWritePerMillionTokens { get; }

    /// <summary>Projects the input/output list prices onto the provider-neutral <see cref="ModelPricing"/> descriptor field.</summary>
    /// <returns>A <see cref="ModelPricing"/> carrying the input and output prices and the currency.</returns>
    public ModelPricing ToModelPricing() => new(InputPerMillionTokens, OutputPerMillionTokens, Currency);

    private static void ThrowIfNegative(decimal? amount, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(amount))] string? paramName = null)
    {
        if (amount is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        }
    }
}
