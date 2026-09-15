// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="OllamaProviderOptions"/> during startup validation and
/// whenever they are first resolved, so a missing endpoint or invalid path
/// fails composition rather than the middle of an agent run.
/// </summary>
public sealed class OllamaProviderOptionsValidator: IValidateOptions<OllamaProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OllamaProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.BaseAddress,
            options.ChatCompletionsPath,
            options.EmbeddingsPath,
            nameof(OllamaProviderOptions.BaseAddress),
            nameof(OllamaProviderOptions.ChatCompletionsPath),
            nameof(OllamaProviderOptions.EmbeddingsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
