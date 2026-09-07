// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="AwsBedrockProviderOptions"/> at the point they are
/// first resolved, so a missing region fails composition rather than the
/// middle of an agent run.
/// </summary>
public sealed class AwsBedrockProviderOptionsValidator: IValidateOptions<AwsBedrockProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AwsBedrockProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.Region)
            ? ValidateOptionsResult.Fail(
                $"{nameof(AwsBedrockProviderOptions.Region)} must be configured with the AWS region hosting " +
                "the request, such as \"us-east-1\".")
            : ValidateOptionsResult.Success;
    }
}
