// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>Records the exact evidence the coordinator hands to a selected queue and returns configured outcomes.</summary>
internal sealed class RecordingInputQueue: IInputQueue
{
    private readonly Func<InputAdmissionRequest, AdmissionId, InputAdmissionResult>? _admit;
    private readonly Func<InputPromotionRequest, InputPromotionResult>? _promote;

    internal RecordingInputQueue(
        Func<InputAdmissionRequest, AdmissionId, InputAdmissionResult>? admit = null,
        Func<InputPromotionRequest, InputPromotionResult>? promote = null)
    {
        _admit = admit;
        _promote = promote;
    }

    internal int AppendCalls { get; private set; }
    internal int PromoteCalls { get; private set; }
    internal InputAdmissionRequest? AppendedRequest { get; private set; }
    internal AdmissionId AppendedAdmissionId { get; private set; }
    internal AgentInput? AppendedEffectiveInput { get; private set; }
    internal InputPreprocessingManifest? AppendedPreprocessing { get; private set; }
    internal DateTimeOffset AppendedAt { get; private set; }
    internal InputPromotionRequest? PromotedRequest { get; private set; }

    public ValueTask<InputAdmissionResult> AppendAsync(
        InputAdmissionRequest request,
        AdmissionId admissionId,
        AgentInput effectiveInput,
        InputPreprocessingManifest preprocessing,
        DateTimeOffset admittedAt,
        CancellationToken cancellationToken = default)
    {
        AppendCalls++;
        AppendedRequest = request;
        AppendedAdmissionId = admissionId;
        AppendedEffectiveInput = effectiveInput;
        AppendedPreprocessing = preprocessing;
        AppendedAt = admittedAt;
        var result = _admit is null
            ? InputCoordinationTestData.Accepted(admissionId, request.Input.Id)
            : _admit(request, admissionId);
        return ValueTask.FromResult(result);
    }

    public ValueTask<InputPromotionResult> PromoteAsync(
        InputPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        PromoteCalls++;
        PromotedRequest = request;
        var result = _promote is null ? InputCoordinationTestData.Rejected() : _promote(request);
        return ValueTask.FromResult(result);
    }
}
