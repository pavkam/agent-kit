// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="CohereProviderOptions"/> at the point they are
/// first resolved, so a missing endpoint or invalid setting fails
/// composition rather than the middle of an agent run.
/// </summary>
public sealed class CohereProviderOptionsValidator: IValidateOptions<CohereProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, CohereProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(CohereProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(CohereProviderOptions.ChatPath)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
