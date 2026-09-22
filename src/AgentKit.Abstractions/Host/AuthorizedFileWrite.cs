// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Binds one authorized file write to its resolved target, disposition,
/// declared content evidence, atomicity requirements, and grant.
/// </summary>
/// <remarks>
/// Declared stream metadata is advisory: the implementation bounds and
/// fingerprints the bytes it actually consumes and compares them with the
/// declaration before making the result visible.
/// </remarks>
public sealed record AuthorizedFileWrite
{
    /// <summary>Initializes a new instance of the <see cref="AuthorizedFileWrite"/> record.</summary>
    /// <param name="resolvedTarget">The resolved target bound by authorization.</param>
    /// <param name="disposition">The explicit write disposition.</param>
    /// <param name="expectedTargetFingerprint">
    /// The expected target fingerprint when the disposition requires one, or
    /// <see langword="null"/> when absent targets are permitted.
    /// </param>
    /// <param name="declaredContentLength">The declared payload length in bytes.</param>
    /// <param name="declaredContentFingerprint">The declared payload fingerprint.</param>
    /// <param name="atomicityMode">The required atomic target-state behavior.</param>
    /// <param name="effectClass">The declared effect class for the write.</param>
    /// <param name="grant">The bounded grant issued for this exact write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="declaredContentLength"/> is negative.
    /// </exception>
    public AuthorizedFileWrite(
        ResolvedFileTarget resolvedTarget,
        FileWriteDisposition disposition,
        ContentHash? expectedTargetFingerprint,
        long declaredContentLength,
        ContentHash declaredContentFingerprint,
        FileWriteAtomicityMode atomicityMode,
        FileWriteEffectClass effectClass,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfNegative(declaredContentLength);
        ResolvedTarget = resolvedTarget;
        Disposition = disposition;
        ExpectedTargetFingerprint = expectedTargetFingerprint;
        DeclaredContentLength = declaredContentLength;
        DeclaredContentFingerprint = declaredContentFingerprint;
        AtomicityMode = atomicityMode;
        EffectClass = effectClass;
        Grant = grant;
    }

    /// <summary>Gets the resolved target bound by authorization.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the explicit write disposition.</summary>
    public FileWriteDisposition Disposition { get; init; }

    /// <summary>Gets the expected target fingerprint when required.</summary>
    public ContentHash? ExpectedTargetFingerprint { get; init; }

    /// <summary>Gets the declared payload length in bytes.</summary>
    public long DeclaredContentLength { get; init; }

    /// <summary>Gets the declared payload fingerprint.</summary>
    public ContentHash DeclaredContentFingerprint { get; init; }

    /// <summary>Gets the required atomic target-state behavior.</summary>
    public FileWriteAtomicityMode AtomicityMode { get; init; }

    /// <summary>Gets the declared effect class for the write.</summary>
    public FileWriteEffectClass EffectClass { get; init; }

    /// <summary>Gets the bounded grant issued for this exact write.</summary>
    public SecurityGrant Grant { get; init; }
}
