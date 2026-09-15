// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="AzureOpenAIProviderOptions"/> during startup
/// validation, so a missing resource endpoint or invalid path fails
/// composition rather than the middle of an agent run.
/// </summary>
/// <remarks>
/// Endpoint checks are shared with every other OpenAI-compatible leaf
/// through <see cref="OpenAICompatibleEndpointOptionsValidation"/>. The
/// only Azure-specific refinement is an explicit "not configured" failure
/// for an absent resource endpoint, which, unlike a public API base
/// address, is account-specific and has no default a caller could have
/// forgotten to override.
/// </remarks>
public sealed class AzureOpenAIProviderOptionsValidator: IValidateOptions<AzureOpenAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AzureOpenAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ResourceEndpoint is null)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(AzureOpenAIProviderOptions.ResourceEndpoint)} must be configured with the caller's " +
                "Azure OpenAI resource endpoint, as an absolute URI.");
        }

        var failures = OpenAICompatibleEndpointOptionsValidation.Validate(
            options.ResourceEndpoint,
            options.ChatCompletionsPath,
            options.EmbeddingsPath,
            nameof(AzureOpenAIProviderOptions.ResourceEndpoint),
            nameof(AzureOpenAIProviderOptions.ChatCompletionsPath),
            nameof(AzureOpenAIProviderOptions.EmbeddingsPath));

        return failures.IsEmpty ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
