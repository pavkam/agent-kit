// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="OpenAIProviderOptions"/> during host startup, so a
/// missing endpoint or invalid path fails composition
/// rather than the middle of an agent run.
/// </summary>
public sealed class OpenAIProviderOptionsValidator: IValidateOptions<OpenAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.BaseAddress,
            options.ChatCompletionsPath,
            options.EmbeddingsPath,
            nameof(OpenAIProviderOptions.BaseAddress),
            nameof(OpenAIProviderOptions.ChatCompletionsPath),
            nameof(OpenAIProviderOptions.EmbeddingsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
