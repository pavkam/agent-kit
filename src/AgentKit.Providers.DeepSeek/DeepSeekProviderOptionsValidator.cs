// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.DeepSeek;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="DeepSeekProviderOptions"/> during startup validation
/// and whenever they are first resolved, so a missing endpoint or invalid path
/// fails composition rather than the middle of an agent run.
/// </summary>
public sealed class DeepSeekProviderOptionsValidator: IValidateOptions<DeepSeekProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DeepSeekProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.BaseAddress,
            options.ChatCompletionsPath,
            nameof(DeepSeekProviderOptions.BaseAddress),
            nameof(DeepSeekProviderOptions.ChatCompletionsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
