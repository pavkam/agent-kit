// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text;

/// <summary>Creates canonical resource and input evidence for one resolved process operation.</summary>
public static class ProcessSecurityBinding
{
    /// <summary>Creates ordered executable, workspace, and sandbox resources for policy and enforcement.</summary>
    /// <param name="intent">The canonical resolved process intent.</param>
    /// <returns>The exact ordered resources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    public static ImmutableArray<ProtectedResource> Resources(ResolvedProcessIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return
        [
            new ProtectedResource(ProtectedResourceKind.Process, intent.AbsoluteExecutablePath),
            new ProtectedResource(
                ProtectedResourceKind.Directory,
                intent.Request.WorkingDirectory?.Value ?? "."),
            new ProtectedResource(
                ProtectedResourceKind.Process,
                $"sandbox:{intent.Request.SandboxProfile.Value}"),
        ];
    }

    /// <summary>Computes exact secret-free authorization evidence for the complete resolved intent.</summary>
    /// <param name="intent">The canonical resolved process intent.</param>
    /// <returns>An algorithm-qualified SHA-256 input fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    public static InputFingerprint Fingerprint(ResolvedProcessIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operation = "process-run-v2",
            operationId = intent.Request.Id.ToString(),
            executablePath = intent.AbsoluteExecutablePath,
            executableFingerprint = intent.ExecutableFingerprint.Value,
            argumentFingerprints = intent.Request.Arguments.Select(FingerprintText),
            workingDirectory = intent.Request.WorkingDirectory?.Value ?? ".",
            environmentNames = intent.Request.Environment.Select(static item => item.Name),
            environmentValueFingerprints = intent.Request.Environment.Select(static item => FingerprintText(item.Value)),
            environmentFingerprint = intent.EnvironmentFingerprint.Value,
            standardInputFingerprint = intent.StandardInputFingerprint.Value,
            sandboxProfile = intent.Request.SandboxProfile.Value,
            workspaceAccess = intent.Request.WorkspaceAccess,
            sideEffectClass = intent.Request.SideEffectClass,
            childPolicy = intent.Request.ChildPolicy,
            timeoutTicks = intent.Request.Limits.Timeout.Ticks,
            maximumOutputBytes = intent.Request.Limits.MaximumOutputBytes,
            terminationGraceTicks = intent.Request.Limits.TerminationGracePeriod.Ticks,
        });
        return new InputFingerprint($"sha256:{Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }

    /// <summary>Computes a canonical byte-content fingerprint.</summary>
    /// <param name="content">The exact bytes.</param>
    /// <returns>An algorithm-qualified lowercase SHA-256 fingerprint.</returns>
    public static ContentHash FingerprintBytes(ReadOnlySpan<byte> content) => new(
        $"sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant()}");

    /// <summary>Computes a canonical strict UTF-8 text fingerprint without retaining the raw value.</summary>
    /// <param name="value">The non-null text.</param>
    /// <returns>An algorithm-qualified lowercase SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="EncoderFallbackException"><paramref name="value"/> contains invalid Unicode scalar data.</exception>
    public static string FingerprintText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FingerprintBytes(new UTF8Encoding(false, true).GetBytes(value)).Value;
    }
}
