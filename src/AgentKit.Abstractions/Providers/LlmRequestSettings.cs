// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Portable sampling and output settings for one chat model request.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Every setting is optional; a <see langword="null"/> value means "use the
/// provider's own default" rather than any specific value, so a request
/// that supplies no settings at all is a legitimate, meaningful request
/// rather than an error.
/// </para>
/// </remarks>
public sealed record LlmRequestSettings
{
    /// <summary>
    /// Gets the shared instance representing no explicit settings; every
    /// value defers to the provider's own default.
    /// </summary>
    public static LlmRequestSettings Default { get; } = new(
        temperature: null,
        topP: null,
        maxOutputTokens: null,
        stopSequences: [],
        parallelToolCalls: null,
        seed: null,
        extensions: ExtensionData.Empty);

    /// <summary>Initializes a new instance of the <see cref="LlmRequestSettings"/> record.</summary>
    /// <param name="temperature">The sampling temperature, when overridden.</param>
    /// <param name="topP">The nucleus sampling probability mass, when overridden.</param>
    /// <param name="maxOutputTokens">The maximum number of output tokens to produce, when overridden.</param>
    /// <param name="stopSequences">Sequences that stop generation when produced.</param>
    /// <param name="parallelToolCalls">
    /// Whether the model may request more than one tool call in a single
    /// response, when overridden.
    /// </param>
    /// <param name="seed">A deterministic sampling seed, when supported and overridden.</param>
    /// <param name="extensions">Provider-specific sampling data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="stopSequences"/> is a default, uninitialized array.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxOutputTokens"/> is negative, or <paramref name="temperature"/> or
    /// <paramref name="topP"/> is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public LlmRequestSettings(
        double? temperature,
        double? topP,
        long? maxOutputTokens,
        ImmutableArray<string> stopSequences,
        bool? parallelToolCalls,
        long? seed,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(stopSequences);
        ArgumentNullException.ThrowIfNull(extensions);

        if (maxOutputTokens is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputTokens),
                maxOutputTokens,
                "Value must not be negative.");
        }

        if (temperature is { } temperatureValue && !double.IsFinite(temperatureValue))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "Value must be finite.");
        }

        if (topP is { } topPValue && !double.IsFinite(topPValue))
        {
            throw new ArgumentOutOfRangeException(nameof(topP), topP, "Value must be finite.");
        }

        Temperature = temperature;
        TopP = topP;
        MaxOutputTokens = maxOutputTokens;
        StopSequences = stopSequences;
        ParallelToolCalls = parallelToolCalls;
        Seed = seed;
        Extensions = extensions;
    }

    /// <summary>Gets the sampling temperature, when overridden.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value assigned during initialization or non-destructive mutation is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public double? Temperature
    {
        get;
        init
        {
            if (value is { } finite)
            {
                ArgumentOutOfRangeException.ThrowIfNotEqual(double.IsFinite(finite), true, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets the nucleus sampling probability mass, when overridden.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value assigned during initialization or non-destructive mutation is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public double? TopP
    {
        get;
        init
        {
            if (value is { } finite)
            {
                ArgumentOutOfRangeException.ThrowIfNotEqual(double.IsFinite(finite), true, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets the maximum number of output tokens to produce, when overridden.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value assigned during initialization or non-destructive mutation is negative.
    /// </exception>
    public long? MaxOutputTokens
    {
        get;
        init
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value.Value, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets the sequences that stop generation when produced.</summary>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is a default,
    /// uninitialized array.
    /// </exception>
    public ImmutableArray<string> StopSequences
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets whether the model may request more than one tool call in a
    /// single response, when overridden.
    /// </summary>
    public bool? ParallelToolCalls { get; init; }

    /// <summary>Gets the deterministic sampling seed, when supported and overridden.</summary>
    public long? Seed { get; init; }

    /// <summary>Gets the portable reasoning-work level requested from a capable model.</summary>
    /// <value>A semantic effort level, or <see langword="null"/> to use the provider's default.</value>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is not defined.</exception>
    public LlmReasoningEffort? ReasoningEffort
    {
        get;
        init
        {
            if (value is { } effort)
            {
                ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(effort), true, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Gets provider-specific sampling data.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(LlmRequestSettings? other) =>
        other is not null
        && Nullable.Equals(Temperature, other.Temperature)
        && Nullable.Equals(TopP, other.TopP)
        && MaxOutputTokens == other.MaxOutputTokens
        && StopSequences.SequenceEqual(other.StopSequences)
        && ParallelToolCalls == other.ParallelToolCalls
        && Seed == other.Seed
        && ReasoningEffort == other.ReasoningEffort
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Temperature);
        hash.Add(TopP);
        hash.Add(MaxOutputTokens);
        foreach (var stopSequence in StopSequences)
        {
            hash.Add(stopSequence);
        }

        hash.Add(ParallelToolCalls);
        hash.Add(Seed);
        hash.Add(ReasoningEffort);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
