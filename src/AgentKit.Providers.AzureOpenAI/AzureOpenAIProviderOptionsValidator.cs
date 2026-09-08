// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="AzureOpenAIProviderOptions"/> at the point they are
/// first resolved, so a missing resource endpoint or invalid path fails
/// composition rather than the middle of an agent run.
/// </summary>
public sealed class AzureOpenAIProviderOptionsValidator: IValidateOptions<AzureOpenAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AzureOpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ResourceEndpoint is null || !options.ResourceEndpoint.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(AzureOpenAIProviderOptions.ResourceEndpoint)} must be configured with the caller's " +
                "Azure OpenAI resource endpoint, as an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath) ||
                !Uri.TryCreate(options.ChatCompletionsPath, UriKind.Relative, out _) ||
                options.ChatCompletionsPath[0] is '/' or '\\'
            ? ValidateOptionsResult.Fail(
                $"{nameof(AzureOpenAIProviderOptions.ChatCompletionsPath)} must be a non-rooted relative URI path.")
            : string.IsNullOrWhiteSpace(options.EmbeddingsPath) ||
                !Uri.TryCreate(options.EmbeddingsPath, UriKind.Relative, out _) ||
                options.EmbeddingsPath[0] is '/' or '\\'
            ? ValidateOptionsResult.Fail(
                $"{nameof(AzureOpenAIProviderOptions.EmbeddingsPath)} must be a non-rooted relative URI path.")
            : ValidateOptionsResult.Success;
    }
}
