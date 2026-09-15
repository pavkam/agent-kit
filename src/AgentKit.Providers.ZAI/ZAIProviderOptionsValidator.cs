// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI;

using AgentKit.Providers.OpenAICompatible;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="ZAIProviderOptions"/> during host startup, so a
/// missing endpoint or invalid path fails composition
/// rather than the middle of an agent run.
/// </summary>
public sealed class ZAIProviderOptionsValidator: IValidateOptions<ZAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ZAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.BaseAddress,
            options.ChatCompletionsPath,
            nameof(ZAIProviderOptions.BaseAddress),
            nameof(ZAIProviderOptions.ChatCompletionsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
