// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="OpenRouterProviderOptions"/> during host startup, so
/// a missing endpoint or invalid path fails composition
/// rather than the middle of an agent run.
/// </summary>
public sealed class OpenRouterProviderOptionsValidator: IValidateOptions<OpenRouterProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenRouterProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(OpenRouterProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath) ||
                !Uri.TryCreate(options.ChatCompletionsPath, UriKind.Relative, out _) ||
                options.ChatCompletionsPath[0] is '/' or '\\'
            ? ValidateOptionsResult.Fail(
                $"{nameof(OpenRouterProviderOptions.ChatCompletionsPath)} must be a non-rooted relative URI path.")
            : string.IsNullOrWhiteSpace(options.EmbeddingsPath) ||
                !Uri.TryCreate(options.EmbeddingsPath, UriKind.Relative, out _) ||
                options.EmbeddingsPath[0] is '/' or '\\'
            ? ValidateOptionsResult.Fail(
                $"{nameof(OpenRouterProviderOptions.EmbeddingsPath)} must be a non-rooted relative URI path.")
            : ValidateOptionsResult.Success;
    }
}
