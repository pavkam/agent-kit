// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one provider-neutral error with safe correlation, retry, effect, and mapper evidence.</summary>
/// <remarks>
/// This immutable value proves only local structural validity. The mapper remains responsible for redacting the safe
/// message, external code, and diagnostics. <see cref="IsRetryable"/> is advisory; an operation owner still evaluates
/// idempotency, visible output, effect certainty, deadlines, limits, and attempt policy.
/// </remarks>
public sealed record AgentError
{
    /// <summary>Creates a portable error from evidence already normalized by the owning boundary.</summary>
    /// <param name="code">The nondefault stable or custom error code.</param>
    /// <param name="safeMessage">A nonblank message the mapper has classified as safe for its intended surface.</param>
    /// <param name="isRetryable">Advisory evidence that retry may be considered by the operation owner.</param>
    /// <param name="sideEffectCertainty">What is known about the relevant external effect.</param>
    /// <param name="origin">The nondefault mapper or effect-boundary provenance.</param>
    /// <param name="externalCode">Safe opaque external machine text, or <see langword="null"/> when unavailable.</param>
    /// <param name="operationId">The nondefault operation correlation when established, or <see langword="null"/>.</param>
    /// <param name="externalRequestId">The nondefault external request correlation when reported, or <see langword="null"/>.</param>
    /// <param name="retryAfter">A normalized nonnegative retry delay, or <see langword="null"/> when absent.</param>
    /// <param name="diagnostics">Safe structured diagnostic evidence retained by the mapper.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> or <paramref name="origin"/> is default; <paramref name="sideEffectCertainty"/> is
    /// undefined; a present <paramref name="operationId"/> or <paramref name="externalRequestId"/> is default; or a
    /// present <paramref name="retryAfter"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="safeMessage"/> or <paramref name="diagnostics"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is empty or whitespace, or a present <paramref name="externalCode"/> is empty or
    /// whitespace.
    /// </exception>
    public AgentError(
        AgentErrorCode code,
        string safeMessage,
        bool isRetryable,
        SideEffectCertainty sideEffectCertainty,
        ErrorOrigin origin,
        string? externalCode,
        OperationId? operationId,
        ExternalRequestId? externalRequestId,
        TimeSpan? retryAfter,
        ExtensionData diagnostics)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(code, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentOutOfRangeException.ThrowIfEqual(origin, default);
        if (externalCode is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(externalCode);
        }

        if (operationId is { } presentOperationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentOperationId, default, nameof(operationId));
        }

        if (externalRequestId is { } presentExternalRequestId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentExternalRequestId, default, nameof(externalRequestId));
        }

        if (retryAfter is { } presentRetryAfter)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(presentRetryAfter, TimeSpan.Zero, nameof(retryAfter));
        }

        ArgumentNullException.ThrowIfNull(diagnostics);

        Code = code;
        SafeMessage = safeMessage;
        IsRetryable = isRetryable;
        SideEffectCertainty = sideEffectCertainty;
        Origin = origin;
        ExternalCode = externalCode;
        OperationId = operationId;
        ExternalRequestId = externalRequestId;
        RetryAfter = retryAfter;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the stable or custom portable category used for typed branching.</summary>
    /// <value>A nondefault exact ordinal code.</value>
    public AgentErrorCode Code { get; }

    /// <summary>Gets the mapper-classified safe description without exposing an original exception.</summary>
    /// <value>Nonblank text whose redaction remains the mapper's responsibility.</value>
    public string SafeMessage { get; }

    /// <summary>Gets whether the operation owner may consider retry under its stricter safety policy.</summary>
    /// <value>Advisory evidence only; <see langword="true"/> never guarantees that retry is safe.</value>
    public bool IsRetryable { get; }

    /// <summary>Gets what the mapper knows about the relevant external effect.</summary>
    /// <value>One defined certainty value, independent of retry advice.</value>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <summary>Gets the mapper or effect boundary that normalized the failure.</summary>
    /// <value>A nondefault extensible provenance identity.</value>
    public ErrorOrigin Origin { get; }

    /// <summary>Gets safe opaque external machine text when the source supplied it.</summary>
    /// <value>Nonblank exact text, or <see langword="null"/> when unavailable.</value>
    public string? ExternalCode { get; }

    /// <summary>Gets the framework operation correlated with the failure when established.</summary>
    /// <value>A nondefault identity, or <see langword="null"/>.</value>
    public OperationId? OperationId { get; }

    /// <summary>Gets the external system's request correlation when reported.</summary>
    /// <value>A nondefault identity, or <see langword="null"/>.</value>
    public ExternalRequestId? ExternalRequestId { get; }

    /// <summary>Gets the normalized provider or boundary retry hint when supplied.</summary>
    /// <value>A nonnegative delay, including zero, or <see langword="null"/>.</value>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Gets mapper-owned safe structured evidence without an exception or raw response body.</summary>
    /// <value>A nonnull immutable diagnostic bag; its safety remains the mapper's responsibility.</value>
    public ExtensionData Diagnostics { get; }
}
