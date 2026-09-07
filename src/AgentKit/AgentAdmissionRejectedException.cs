// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Thrown when a pinned handle's definition is unavailable for a new admission.</summary>
public sealed class AgentAdmissionRejectedException: InvalidOperationException
{
    /// <summary>Initializes the exception from immutable pre-admission evidence.</summary>
    /// <param name="rejection">The non-null, content-free admission evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is <see langword="null"/>.</exception>
    public AgentAdmissionRejectedException(AgentAdmissionRejection rejection)
        : base(GetMessage(rejection))
        => Rejection = rejection;

    /// <summary>Gets the immutable evidence describing the rejected pre-admission.</summary>
    public AgentAdmissionRejection Rejection { get; }

    private static string GetMessage(AgentAdmissionRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        return rejection.Reason;
    }
}
