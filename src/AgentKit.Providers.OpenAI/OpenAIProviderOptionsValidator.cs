// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="OpenAIProviderOptions"/> at the point they are
/// first resolved, so a missing endpoint or invalid path fails composition
/// rather than the middle of an agent run.
/// </summary>
public sealed class OpenAIProviderOptionsValidator: IValidateOptions<OpenAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(OpenAIProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(OpenAIProviderOptions.ChatCompletionsPath)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
