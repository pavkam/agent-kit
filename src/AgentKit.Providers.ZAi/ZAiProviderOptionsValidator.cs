// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="ZAiProviderOptions"/> during host startup, so a
/// missing endpoint or invalid path fails composition
/// rather than the middle of an agent run.
/// </summary>
public sealed class ZAiProviderOptionsValidator: IValidateOptions<ZAiProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ZAiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(ZAiProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath) ||
                !Uri.TryCreate(options.ChatCompletionsPath, UriKind.Relative, out _) ||
                options.ChatCompletionsPath[0] is '/' or '\\'
            ? ValidateOptionsResult.Fail(
                $"{nameof(ZAiProviderOptions.ChatCompletionsPath)} must be a non-rooted relative URI path.")
            : ValidateOptionsResult.Success;
    }
}
