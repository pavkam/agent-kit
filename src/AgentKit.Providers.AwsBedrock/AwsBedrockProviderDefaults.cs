// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// The fixed identity, endpoint, and capability defaults for the Amazon
/// Bedrock Runtime Converse integration.
/// </summary>
/// <remarks>
/// This package covers only the model-agnostic <c>Converse</c> and
/// <c>ConverseStream</c> operations. Bedrock's model-specific
/// <c>InvokeModel</c>/<c>InvokeModelWithResponseStream</c> operations (with
/// their per-provider request/response bodies), Knowledge Bases, Agents,
/// Guardrails, and model-management control-plane APIs are separate
/// contracts not covered by this package. Image, document, video, audio,
/// guardrail, citation, and prompt-caching content blocks are also out of
/// scope for this pass.
/// </remarks>
public static class AwsBedrockProviderDefaults
{
    /// <summary>Gets the stable <see cref="ProviderId"/> for Amazon Bedrock.</summary>
    public static ProviderId ProviderId { get; } = new("aws-bedrock");

    /// <summary>Gets the stable <see cref="ApiFamilyId"/> for Bedrock's Converse wire format.</summary>
    public static ApiFamilyId ApiFamily { get; } = new("aws-bedrock-converse");

    /// <summary>The AWS signing service name used in the SigV4 credential scope.</summary>
    public const string SigningServiceName = "bedrock";

    /// <summary>
    /// Gets the default capability set applied to a registered Bedrock
    /// model unless the caller supplies its own.
    /// </summary>
    /// <remarks>
    /// Tool call support and parallel tool calls vary by underlying
    /// foundation model; callers registering a model whose provider does
    /// not support tool use, or does not support issuing more than one
    /// tool call per turn, should supply their own
    /// <see cref="ModelCapabilities"/> rather than relying on this shared
    /// default. Reasoning content blocks, vision input, and native
    /// structured output are not yet translated by this package, so those
    /// capabilities remain <see langword="false"/> here regardless of the
    /// underlying model's own support.
    /// </remarks>
    public static ModelCapabilities DefaultCapabilities { get; } = new(
        supportsSystemInstructions: true,
        supportsStreaming: true,
        supportsToolCalls: true,
        supportsParallelToolCalls: true,
        supportsStructuredOutput: false,
        supportsReasoning: false,
        supportsVisionInput: false,
        ExtensionData.Empty);

    /// <summary>
    /// Gets the default, unbounded model limits applied to a registered
    /// Bedrock model unless the caller supplies its own.
    /// </summary>
    public static ModelLimits DefaultLimits { get; } = new(maxContextTokens: null, maxOutputTokens: null);

    /// <summary>
    /// Builds the Bedrock Runtime regional base address for
    /// <paramref name="region"/>, such as <c>https://bedrock-runtime.us-east-1.amazonaws.com</c>.
    /// </summary>
    /// <param name="region">The AWS region hosting the request, such as <c>us-east-1</c>.</param>
    /// <returns>The regional Bedrock Runtime base address.</returns>
    /// <exception cref="ArgumentException"><paramref name="region"/> is null, empty, or whitespace.</exception>
    public static Uri BuildBaseAddress(string? region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        return new Uri($"https://bedrock-runtime.{region}.amazonaws.com");
    }

    /// <summary>
    /// Builds the absolute buffered <c>Converse</c> operation URI for
    /// <paramref name="modelId"/>.
    /// </summary>
    /// <param name="options">The validated Bedrock provider options.</param>
    /// <param name="modelId">
    /// The Bedrock model ID, inference profile ID, or ARN to invoke, as
    /// documented on the <c>Converse</c> operation's <c>modelId</c> URI
    /// parameter.
    /// </param>
    /// <returns>The absolute URI of the buffered Converse operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="modelId"/> is null, empty, or whitespace.</exception>
    public static Uri BuildConverseUri(AwsBedrockProviderOptions options, string modelId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        return new Uri(BuildBaseAddress(options.Region), $"/model/{EscapeModelId(modelId)}/converse");
    }

    /// <summary>
    /// Builds the absolute streaming <c>ConverseStream</c> operation URI
    /// for <paramref name="modelId"/>.
    /// </summary>
    /// <param name="options">The validated Bedrock provider options.</param>
    /// <param name="modelId">
    /// The Bedrock model ID, inference profile ID, or ARN to invoke, as
    /// documented on the <c>ConverseStream</c> operation's <c>modelId</c>
    /// URI parameter.
    /// </param>
    /// <returns>The absolute URI of the streaming ConverseStream operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="modelId"/> is null, empty, or whitespace.</exception>
    public static Uri BuildConverseStreamUri(AwsBedrockProviderOptions options, string modelId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        return new Uri(BuildBaseAddress(options.Region), $"/model/{EscapeModelId(modelId)}/converse-stream");
    }

    /// <summary>
    /// Escapes a Bedrock model ID, inference profile ID, or ARN for
    /// embedding in a <c>Converse</c>/<c>ConverseStream</c> request path.
    /// </summary>
    /// <remarks>
    /// The Converse <c>modelId</c> URI label is a single non-greedy path
    /// segment, so every character that would otherwise be interpreted as a
    /// path separator or reserved delimiter must be percent-encoded, not
    /// only the colon that separates a versioned model ID's version suffix
    /// (<c>anthropic.claude-3-sonnet-20240229-v1:0</c>). ARNs for inference
    /// profiles, application inference profiles, provisioned/custom models,
    /// and prompts all contain <c>/</c>
    /// (e.g. <c>...:inference-profile/us.anthropic.claude-...</c>); leaving
    /// it unescaped splits the label into extra path segments that no
    /// longer match <c>/model/{modelId}/converse</c> and get
    /// routed/validated as a different resource.
    /// <see cref="Uri.EscapeDataString(string)"/> percent-encodes both
    /// characters (and every other reserved character) in one pass.
    /// <see cref="AwsSigV4Signer"/> canonicalizes whatever wire path this
    /// method produces by encoding each already-escaped segment a second
    /// time, as SigV4 requires for non-S3 services, so the escaped form
    /// chosen here and the signed form stay consistent by construction.
    /// </remarks>
    private static string EscapeModelId(string modelId) => Uri.EscapeDataString(modelId);
}
