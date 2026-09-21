// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using Microsoft.Extensions.Options;

/// <summary>Estimates tokens from UTF-16 text length using configured options.</summary>
internal sealed class CharacterBasedContextMessageTokenEstimator: IContextMessageTokenEstimator
{
    private readonly double _charactersPerToken;

    /// <summary>Initializes the estimator from validated context options.</summary>
    /// <param name="options">The context options carrying the character-per-token ratio.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public CharacterBasedContextMessageTokenEstimator(IOptions<AgentContextOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _charactersPerToken = options.Value.EstimatedCharactersPerToken;
    }

    /// <inheritdoc/>
    public long EstimateTokens(ImmutableArray<AgentMessage> messages) =>
        ContextMessageTokenEstimation.EstimateTokens(messages, _charactersPerToken);

    /// <inheritdoc/>
    public long EstimateTokens(ImmutableArray<ContentPart> parts) =>
        ContextMessageTokenEstimation.EstimateTokens(parts, _charactersPerToken);
}
