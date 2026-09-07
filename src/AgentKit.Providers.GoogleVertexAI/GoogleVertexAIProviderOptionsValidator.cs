// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates <see cref="GoogleVertexAIProviderOptions"/> at the point they
/// are first resolved, so a missing project, region, or invalid setting
/// fails composition rather than the middle of an agent run.
/// </summary>
public sealed class GoogleVertexAIProviderOptionsValidator: IValidateOptions<GoogleVertexAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GoogleVertexAIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.ProjectId)
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleVertexAIProviderOptions.ProjectId)} must be configured with the caller's " +
                "Google Cloud project ID.")
            : string.IsNullOrWhiteSpace(options.Location)
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleVertexAIProviderOptions.Location)} must be configured with the Google Cloud " +
                "region hosting the request, such as \"us-central1\".")
            : string.IsNullOrWhiteSpace(options.Publisher)
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleVertexAIProviderOptions.Publisher)} must not be null, empty, or whitespace.")
            : string.IsNullOrWhiteSpace(options.ApiVersion)
            ? ValidateOptionsResult.Fail(
                $"{nameof(GoogleVertexAIProviderOptions.ApiVersion)} must not be null, empty, or whitespace.")
            : ValidateOptionsResult.Success;
    }
}
