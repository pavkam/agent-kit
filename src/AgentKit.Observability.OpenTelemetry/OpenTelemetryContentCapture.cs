// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

using System.Security.Cryptography;
using System.Text;

/// <summary>Extracts classified content from run events and routes it through the configured redactor, failing closed.</summary>
/// <remarks>
/// Content is never exported without a successful <see cref="RedactedContent"/> result that still satisfies the exporter's
/// classification and size policy. A redactor exception, an out-of-policy result, a disallowed classification, or any
/// unknown <see cref="RedactionResult"/> variant omits the content field; the structural event is still exported. The
/// omission reason is bounded and never contains content.
/// </remarks>
internal static class OpenTelemetryContentCapture
{
    internal const string ReasonClassificationNotAllowed = "classification_not_allowed";
    internal const string ReasonRedactorOmitted = "redactor_omitted";
    internal const string ReasonRedactionFailed = "redaction_failed";
    internal const string ReasonPolicyViolation = "policy_violation";

    /// <summary>Returns the bounded, classified content a run event carries, or null when the event carries none.</summary>
    /// <param name="runEvent">The non-null run event.</param>
    /// <param name="options">The exporter snapshot supplying classification and bounds.</param>
    /// <returns>The content owned by the new value, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static ObservationContent? Extract(RunEvent runEvent, OpenTelemetryObservationOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        ArgumentNullException.ThrowIfNull(options);
        if (runEvent is not ContentDeltaEvent { Delta: var delta })
        {
            return null;
        }

        (ObservationContentKind Kind, string? Text) extracted = delta switch
        {
            TextContentDelta textDelta => (ObservationContentKind.ModelOutput, textDelta.Text),
            ReasoningContentDelta reasoning => (ObservationContentKind.Reasoning, reasoning.Text),
            ToolArgumentsContentDelta arguments => (ObservationContentKind.ToolArguments, arguments.JsonFragment),
            StructuredDataContentDelta structured => (ObservationContentKind.ModelOutput, structured.JsonFragment),
            _ => (ObservationContentKind.ModelOutput, null),
        };
        var (kind, text) = extracted;
        if (text is null)
        {
            return null;
        }

        var bytes = Bound(Encoding.UTF8.GetBytes(text), options.Bounds.MaximumBytesPerField);
        return new ObservationContent(
            kind,
            options.ContentClassification,
            ImmutableArray.Create(bytes),
            new ContentFingerprint("sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    /// <summary>Routes one content field through the redactor and enforces the exporter policy on the result.</summary>
    /// <param name="redactor">The effective singular redactor.</param>
    /// <param name="options">The exporter snapshot.</param>
    /// <param name="content">The content to redact.</param>
    /// <param name="cancellationToken">Cancels redaction; this is the only exception that propagates.</param>
    /// <returns>The exportable content, or an omission with a bounded reason.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal static async ValueTask<OpenTelemetryContentCaptureOutcome> CaptureAsync(
        IObservationRedactor redactor,
        OpenTelemetryObservationOptionsSnapshot options,
        ObservationContent content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(content);
        if (!options.AllowedClassifications.Contains(content.Classification))
        {
            return new OpenTelemetryContentCaptureOutcome(null, ReasonClassificationNotAllowed);
        }

        RedactionResult? result;
        try
        {
            var policy = new ObservationPolicy(options.Bounds, options.AllowedClassifications);
            result = await redactor.RedactAsync(content, policy, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new OpenTelemetryContentCaptureOutcome(null, ReasonRedactionFailed);
        }

        if (result is not RedactedContent redacted)
        {
            return new OpenTelemetryContentCaptureOutcome(null, ReasonRedactorOmitted);
        }

        var accepted = redacted.Content;
        return accepted.Kind == content.Kind
            && options.AllowedClassifications.Contains(accepted.Classification)
            && accepted.Value.Length <= options.Bounds.MaximumBytesPerField
                ? new OpenTelemetryContentCaptureOutcome(accepted, null)
                : new OpenTelemetryContentCaptureOutcome(null, ReasonPolicyViolation);
    }

    private static byte[] Bound(byte[] bytes, int maximum) =>
        bytes.Length <= maximum ? bytes : bytes[..maximum];
}
