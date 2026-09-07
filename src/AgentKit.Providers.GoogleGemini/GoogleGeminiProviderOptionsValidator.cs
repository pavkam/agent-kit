// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="GoogleGeminiProviderOptions"/> at the point they are
/// first resolved, so a missing endpoint or invalid setting fails
/// composition rather than the middle of an agent run.
/// </summary>
public sealed class GoogleGeminiProviderOptionsValidator: IValidateOptions<GoogleGeminiProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GoogleGeminiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleGeminiProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ApiVersion)
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleGeminiProviderOptions.ApiVersion)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
