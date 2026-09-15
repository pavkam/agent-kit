// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using AgentKit.Providers.OpenAICompatible;

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

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.BaseAddress,
            options.ChatCompletionsPath,
            options.EmbeddingsPath,
            nameof(OpenRouterProviderOptions.BaseAddress),
            nameof(OpenRouterProviderOptions.ChatCompletionsPath),
            nameof(OpenRouterProviderOptions.EmbeddingsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
