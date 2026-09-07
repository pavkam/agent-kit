// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;

/// <summary>Produces canonical security evidence for human-question publication.</summary>
public static class HumanQuestionSecurityBinding
{
    /// <summary>Creates the exact application-state resource for one question.</summary>
    /// <param name="id">The question identity.</param>
    /// <returns>The canonical protected resource.</returns>
    public static ProtectedResource Resource(QuestionId id) =>
        new(ProtectedResourceKind.ApplicationState, $"human-question:{id}");

    /// <summary>Fingerprints every behaviorally meaningful field of one proposed question.</summary>
    /// <param name="id">The question identity.</param>
    /// <param name="prompt">The exact prompt.</param>
    /// <param name="options">The ordered exact options.</param>
    /// <param name="allowsFreeText">Whether supplementary text is accepted.</param>
    /// <param name="deadline">The exclusive answer deadline.</param>
    /// <returns>A deterministic SHA-256 input fingerprint.</returns>
    /// <exception cref="ArgumentException"><paramref name="prompt"/> is blank or <paramref name="options"/> is default.</exception>
    public static InputFingerprint Fingerprint(
        QuestionId id,
        string prompt,
        ImmutableArray<HumanQuestionOption> options,
        bool allowsFreeText,
        DateTimeOffset deadline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfDefault(options);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = id.ToString(),
            prompt,
            options = options.Select(static option => new
            {
                id = option.Id.Value,
                option.Label,
                option.Description,
            }),
            allowsFreeText,
            deadline = deadline.ToUniversalTime().ToString("O"),
        });
        return new InputFingerprint(Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
