// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Captures the process-local choices used to compose the example's next conversation runtime.</summary>
internal sealed record CodingAgentConfiguration
{
    /// <summary>Gets the OpenAI models intentionally demonstrated by this sample.</summary>
    public static ImmutableArray<CodingAgentModelOption> Models { get; } =
    [
        new("Terra", "gpt-5.6-terra", "Fast, balanced coding work"),
        new("Sol", "gpt-5.6-sol", "Stronger implementation and debugging"),
        new("Astra", "gpt-6-astra", "Deep reasoning for demanding changes"),
    ];

    /// <summary>Gets reasoning choices in the user-facing order used by menus and the slider.</summary>
    public static ImmutableArray<LlmReasoningEffort> ReasoningEfforts { get; } =
    [
        LlmReasoningEffort.None,
        LlmReasoningEffort.Low,
        LlmReasoningEffort.Medium,
        LlmReasoningEffort.High,
        LlmReasoningEffort.ExtraHigh,
    ];

    /// <summary>Initializes one validated runtime configuration.</summary>
    /// <param name="modelId">The exact OpenAI model identifier.</param>
    /// <param name="reasoningEffort">The portable reasoning effort sent with each model request.</param>
    /// <param name="maximumTurns">The positive tool-calling turn limit for one run.</param>
    /// <param name="readOnlyToolchainRoots">Absolute external directories exposed read-only to sandboxed commands.</param>
    /// <exception cref="ArgumentException"><paramref name="modelId"/> is blank or the roots array is uninitialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reasoningEffort"/> is unknown or <paramref name="maximumTurns"/> is not positive.</exception>
    public CodingAgentConfiguration(
        string modelId,
        LlmReasoningEffort reasoningEffort,
        int maximumTurns,
        ImmutableArray<string> readOnlyToolchainRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(reasoningEffort), true, nameof(reasoningEffort));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTurns);
        ArgumentException.ThrowIfDefault(readOnlyToolchainRoots);

        ModelId = modelId;
        ReasoningEffort = reasoningEffort;
        MaximumTurns = maximumTurns;
        ReadOnlyToolchainRoots = readOnlyToolchainRoots;
    }

    /// <summary>Gets the exact OpenAI model identifier registered for the next runtime.</summary>
    public string ModelId { get; init; }

    /// <summary>Gets the portable reasoning effort applied to every request.</summary>
    public LlmReasoningEffort ReasoningEffort { get; init; }

    /// <summary>Gets the maximum tool-calling turns allowed in one run.</summary>
    public int MaximumTurns { get; init; }

    /// <summary>Gets the selected external roots exposed read-only to sandboxed command processes.</summary>
    public ImmutableArray<string> ReadOnlyToolchainRoots { get; init; }

    /// <summary>Builds the initial configuration from the explicit environment and host root declarations.</summary>
    /// <returns>A configuration suitable for composing the first conversation.</returns>
    public static CodingAgentConfiguration CreateDefault()
    {
        var environmentModel = OpenAiEnvironment.ModelId();
        var model = Models.Any(option => string.Equals(option.ModelId, environmentModel, StringComparison.Ordinal))
            ? environmentModel
            : Models[0].ModelId;
        return new CodingAgentConfiguration(
            model,
            LlmReasoningEffort.None,
            maximumTurns: 12,
            CodingAgentHostEnvironment.ToolchainRoots());
    }
}
