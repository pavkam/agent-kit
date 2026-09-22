// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Produces canonical resources and fingerprints for tool-invocation authorization at the executor boundary.</summary>
public static class ToolInvocationSecurityBinding
{
    /// <summary>Gets the security audience for executor-level invocation grants.</summary>
    /// <value>The component identity that consumes invocation grants before calling <see cref="IToolInvoker"/>.</value>
    public static ComponentId SecurityAudience { get; } = new("agentkit.tools.executor");

    /// <summary>Creates the exact application-state resource for one tool invocation attempt.</summary>
    /// <param name="toolId">The resolved canonical tool identity.</param>
    /// <param name="toolVersion">The resolved exact tool version.</param>
    /// <returns>The canonical application-state resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public static ProtectedResource Resource(ToolId toolId, ToolVersion toolVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"tool-invocation:{toolId.Value}:{toolVersion.Value}");
    }

    /// <summary>Fingerprints one validated invocation attempt.</summary>
    /// <param name="callId">The provider-correlated call identity.</param>
    /// <param name="validatedArguments">The validated canonical arguments fingerprint.</param>
    /// <returns>The deterministic fingerprint bound to this attempt.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public static InputFingerprint InvocationFingerprint(ToolCallId callId, InputFingerprint validatedArguments)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(callId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(validatedArguments, default);
        return Fingerprint(new { callId = callId.Value, arguments = validatedArguments.Value });
    }

    /// <summary>Maps declared tool effects to the protected operation kind used for invocation authorization.</summary>
    /// <param name="effect">The declared tool effect.</param>
    /// <returns>The matching operation kind.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effect"/> is undefined.</exception>
    public static SecurityOperationKind OperationKind(ToolEffect effect) =>
        effect switch
        {
            ToolEffect.ReadOnly => SecurityOperationKind.StateRead,
            ToolEffect.Mutating => SecurityOperationKind.StateMutation,
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };

    /// <summary>Maps declared tool effects to the security effect used for invocation authorization.</summary>
    /// <param name="effect">The declared tool effect.</param>
    /// <returns>The matching security effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effect"/> is undefined.</exception>
    public static SecurityEffect ToSecurityEffect(ToolEffect effect) =>
        effect switch
        {
            ToolEffect.ReadOnly => SecurityEffect.Observe,
            ToolEffect.Mutating => SecurityEffect.Mutate,
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };

    /// <summary>Fingerprints bounded raw admission bytes.</summary>
    /// <param name="rawArguments">The initialized raw argument bytes.</param>
    /// <returns>The deterministic admission fingerprint.</returns>
    /// <exception cref="ArgumentException"><paramref name="rawArguments"/> is uninitialized.</exception>
    public static InputFingerprint RawAdmissionFingerprint(ImmutableArray<byte> rawArguments)
    {
        ArgumentException.ThrowIfDefault(rawArguments);
        return FingerprintBytes(rawArguments.AsSpan());
    }

    /// <summary>Fingerprints validated canonical arguments.</summary>
    /// <param name="arguments">The validated canonical JSON value.</param>
    /// <returns>The deterministic validated-input fingerprint.</returns>
    public static InputFingerprint ValidatedArgumentsFingerprint(JsonElement arguments) =>
        Fingerprint(new { arguments = arguments.GetRawText() });

    private static InputFingerprint Fingerprint<T>(T value) =>
        new(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))));

    private static InputFingerprint FingerprintBytes(ReadOnlySpan<byte> bytes) =>
        new(Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
