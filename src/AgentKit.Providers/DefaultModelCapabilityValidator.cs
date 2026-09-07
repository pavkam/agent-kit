// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The first-party capability validator. It compares a request's stated
/// requirements with a descriptor's declared capabilities and never contacts
/// a provider.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is stateless, deterministic, and thread-safe, so it is
/// registered as a singleton.
/// </para>
/// <para>
/// Under <see cref="CapabilityDowngradePolicy.AllowDeclaredAdjustments"/> only
/// genuinely optional behaviors are adjusted away. Streaming and parallel tool
/// calls are transport and efficiency concerns whose removal changes how a
/// response arrives, not whether it is correct. Tool calls, structured output,
/// vision input, system instructions, and context window are never downgraded,
/// because a request that needs them produces a wrong answer without them
/// rather than a slower one.
/// </para>
/// </remarks>
internal sealed class DefaultModelCapabilityValidator: IModelCapabilityValidator
{
    /// <inheritdoc/>
    public ValueTask<CapabilityValidationResult> ValidateAsync(
        ModelDescriptor model,
        ModelRequirements requirements,
        CapabilityDowngradePolicy downgradePolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentOutOfRangeException.ThrowIfUndefined(downgradePolicy);
        cancellationToken.ThrowIfCancellationRequested();

        var allowAdjustments =
            downgradePolicy is CapabilityDowngradePolicy.AllowDeclaredAdjustments;

        var unsupported = ImmutableArray.CreateBuilder<UnsupportedCapability>();
        var adjustments = ImmutableArray.CreateBuilder<CapabilityAdjustment>();

        var capabilities = model.Capabilities;

        if (requirements.RequiresSystemInstructions && !capabilities.SupportsSystemInstructions)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.SystemInstructions,
                "The request requires a distinct system or developer instruction role."));
        }

        if (requirements.RequiresToolCalls && !capabilities.SupportsToolCalls)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.ToolCalls,
                "The request requires model-requested tool calls."));
        }

        if (requirements.RequiresStructuredOutput && !capabilities.SupportsStructuredOutput)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.StructuredOutput,
                "The request requires provider-enforced structured output."));
        }

        if (requirements.RequiresReasoning && !capabilities.SupportsReasoning)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.Reasoning,
                "The request requires reasoning support."));
        }

        if (requirements.RequiresVisionInput && !capabilities.SupportsVisionInput)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.VisionInput,
                "The request requires image input."));
        }

        if (requirements.MinimumInputTokens is { } minimumTokens
            && model.Limits.MaxContextTokens is { } maxContextTokens
            && maxContextTokens < minimumTokens)
        {
            unsupported.Add(new UnsupportedCapability(
                ModelCapabilityKind.ContextWindow,
                "The model's context window is smaller than the request's stated minimum."));
        }

        if (requirements.RequiresStreaming && !capabilities.SupportsStreaming)
        {
            if (allowAdjustments)
            {
                adjustments.Add(new CapabilityAdjustment(
                    ModelCapabilityKind.Streaming,
                    "Streaming was disabled; the response is delivered as one terminal result."));
            }
            else
            {
                unsupported.Add(new UnsupportedCapability(
                    ModelCapabilityKind.Streaming,
                    "The request requires incremental response streaming."));
            }
        }

        if (requirements.RequiresParallelToolCalls && !capabilities.SupportsParallelToolCalls)
        {
            // Parallel tool calls are only downgradable when the model can
            // still request tools at all; otherwise the missing ToolCalls
            // capability above is the real, non-downgradable failure.
            if (allowAdjustments && capabilities.SupportsToolCalls)
            {
                adjustments.Add(new CapabilityAdjustment(
                    ModelCapabilityKind.ParallelToolCalls,
                    "Parallel tool calls were disabled; tools are requested one at a time."));
            }
            else
            {
                unsupported.Add(new UnsupportedCapability(
                    ModelCapabilityKind.ParallelToolCalls,
                    "The request requires several tool calls in one response."));
            }
        }

        CapabilityValidationResult result = unsupported.Count > 0
            ? new CapabilitiesUnsupported(unsupported.ToImmutable())
            : adjustments.Count > 0
                ? new CapabilitiesDowngraded(adjustments.ToImmutable())
                : new CapabilitiesSupported();

        return ValueTask.FromResult(result);
    }
}
