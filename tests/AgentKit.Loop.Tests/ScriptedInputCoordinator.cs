// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>An <see cref="IInputCoordinator"/> test double that scripts one <see cref="PromoteAsync"/> result per call.</summary>
/// <remarks>
/// <see cref="AdmitAsync"/> is unsupported: no loop behavior under test admits input, it only promotes
/// already-admitted input. Each call to <see cref="PromoteAsync"/> consumes the next scripted factory supplied
/// to the constructor, building the result from the exact request the loop sent so the returned evidence is
/// always self-consistent, and falls back to a stable "nothing eligible" rejection once the script is exhausted.
/// </remarks>
internal sealed class ScriptedInputCoordinator: IInputCoordinator
{
    private readonly Queue<Func<InputPromotionRequest, InputPromotionResult>> _results;

    /// <summary>Initializes a coordinator that returns each scripted result in order, then a stable rejection.</summary>
    /// <param name="results">One factory per call to <see cref="PromoteAsync"/>, applied to the exact received request.</param>
    public ScriptedInputCoordinator(params IEnumerable<Func<InputPromotionRequest, InputPromotionResult>> results) =>
        _results = new Queue<Func<InputPromotionRequest, InputPromotionResult>>(results);

    /// <summary>Gets every promotion request this fake received, in call order.</summary>
    public List<InputPromotionRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitAsync(InputAdmissionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake only scripts promotion.");

    /// <inheritdoc/>
    public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);
        var result = _results.Count > 0
            ? _results.Dequeue()(request)
            : new InputPromotionRejected(new InputRejection(InputRejectionKind.NoEligibleInput, "the script is exhausted"));
        return ValueTask.FromResult(result);
    }

    /// <summary>Builds a committed promotion result carrying one text user message, matching the exact incoming request's selection.</summary>
    /// <param name="request">The exact request this result answers.</param>
    /// <param name="text">The promoted message's text content.</param>
    /// <param name="sessionVersion">The session version to report as observed after the commit.</param>
    /// <param name="operationStateRevision">The total-state revision to report as installed by the commit.</param>
    public static InputPromoted Promoted(
        InputPromotionRequest request, string text, SessionVersion sessionVersion, OperationStateRevision operationStateRevision)
    {
        ArgumentNullException.ThrowIfNull(request);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var payload = new AgentInput(
            new InputId(Guid.NewGuid()), InputDelivery.Steer, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var manifest = new InputPreprocessingManifest(
            new ConfigurationVersion(1), InputPayloadFingerprint.Create(payload), InputPayloadFingerprint.Create(payload));
        var admitted = new AdmittedInput(
            admissionId, request.AgentId, request.SessionId, request.ExecutionLaneId, request.Identity,
            new SessionSequence(Math.Max(request.CutoffSequence.Value, 1)), payload, payload, manifest,
            DateTimeOffset.UnixEpoch, new SessionSequence(request.CutoffSequence.Value + 1));
        var snapshot = new InputPromotionSnapshot(
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.ExpectedOperation,
            request.OperationStateRevision, request.BranchCursor, request.CutoffSequence, request.ExpectedVersion,
            request.ExpectedFencingToken, request.Boundary, request.PreviousTurnId, request.TargetTurnId, [admissionId]);
        return new InputPromoted(
            snapshot, [admitted], sessionVersion, new SessionBranchCursor(request.BranchCursor.BranchId, null), operationStateRevision);
    }
}
