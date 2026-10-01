// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the secret-free evidence and the credential-read grant for one credential resolution.</summary>
/// <remarks>
/// The request holds only version-retained profile snapshots, the captured operation context, the attempt identity, the
/// attempt deadline, and the single-use <see cref="CredentialGrant"/>. It never carries a secret.
/// </remarks>
public sealed record ProviderCredentialResolutionRequest
{
    /// <summary>Initializes a credential resolution request.</summary>
    /// <param name="endpoint">The endpoint profile snapshot selected for the attempt.</param>
    /// <param name="credential">The credential profile snapshot whose source is being asked for a lease.</param>
    /// <param name="operation">The captured semantic operation whose authorization context issued the grant.</param>
    /// <param name="attempt">The one-based attempt number.</param>
    /// <param name="deadline">The absolute attempt deadline.</param>
    /// <param name="credentialGrant">The credential-read grant the source must validate and consume.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is less than one.</exception>
    public ProviderCredentialResolutionRequest(
        ProviderEndpointProfileSnapshot endpoint,
        ProviderCredentialProfileSnapshot credential,
        ProtectedSemanticOperationContext operation,
        int attempt,
        DateTimeOffset deadline,
        SecurityGrant credentialGrant)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(credentialGrant);

        Endpoint = endpoint;
        Credential = credential;
        Operation = operation;
        Attempt = attempt;
        Deadline = deadline;
        CredentialGrant = credentialGrant;
    }

    /// <summary>Gets the endpoint profile snapshot selected for the attempt.</summary>
    public ProviderEndpointProfileSnapshot Endpoint { get; init; }

    /// <summary>Gets the credential profile snapshot whose source is being asked for a lease.</summary>
    public ProviderCredentialProfileSnapshot Credential { get; init; }

    /// <summary>Gets the captured semantic operation whose authorization context issued the grant.</summary>
    public ProtectedSemanticOperationContext Operation { get; init; }

    /// <summary>Gets the one-based attempt number.</summary>
    public int Attempt { get; init; }

    /// <summary>Gets the absolute attempt deadline.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>Gets the credential-read grant the source must validate and consume before reading secrets.</summary>
    public SecurityGrant CredentialGrant { get; init; }
}
