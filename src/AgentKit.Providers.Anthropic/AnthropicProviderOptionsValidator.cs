// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="AnthropicProviderOptions"/> at the point they are
/// first resolved, so a missing endpoint or invalid setting fails
/// composition rather than the middle of an agent run.
/// </summary>
public sealed class AnthropicProviderOptionsValidator: IValidateOptions<AnthropicProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AnthropicProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(AnthropicProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.MessagesPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(AnthropicProviderOptions.MessagesPath)} must not be null, empty, or whitespace.")
            : string.IsNullOrWhiteSpace(options.AnthropicVersion)
            ? ValidateOptionsResult.Fail(
                $"{nameof(AnthropicProviderOptions.AnthropicVersion)} must not be null, empty, or whitespace.")
            : options.DefaultMaxOutputTokens <= 0
            ? ValidateOptionsResult.Fail(
                $"{nameof(AnthropicProviderOptions.DefaultMaxOutputTokens)} must be greater than zero.")
            : ValidateOptionsResult.Success;
    }
}
