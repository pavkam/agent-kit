// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MoonshotKimi;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="MoonshotKimiProviderOptions"/> during startup validation
/// and whenever they are first resolved, so a missing endpoint or invalid path
/// fails composition rather than the middle of an agent run.
/// </summary>
public sealed class MoonshotKimiProviderOptionsValidator: IValidateOptions<MoonshotKimiProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MoonshotKimiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri
            ? ValidateOptionsResult.Fail(
                $"{nameof(MoonshotKimiProviderOptions.BaseAddress)} must be an absolute URI.")
            : string.IsNullOrWhiteSpace(options.ChatCompletionsPath)
            ? ValidateOptionsResult.Fail(
                $"{nameof(MoonshotKimiProviderOptions.ChatCompletionsPath)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
