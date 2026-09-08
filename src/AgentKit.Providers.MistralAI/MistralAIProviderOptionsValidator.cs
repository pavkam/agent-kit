// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="MistralAIProviderOptions"/> at the point they are
/// first resolved, so a missing endpoint or invalid setting fails
/// composition rather than the middle of an agent run.
/// </summary>
public sealed class MistralAIProviderOptionsValidator: IValidateOptions<MistralAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MistralAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(MistralAIProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(MistralAIProviderOptions.ChatCompletionsPath)} must not be null, empty, or whitespace.")
            : string.IsNullOrWhiteSpace(options.EmbeddingsPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(MistralAIProviderOptions.EmbeddingsPath)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
