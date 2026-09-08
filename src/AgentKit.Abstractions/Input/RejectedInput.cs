// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports typed pre-admission rejection without fabricating durable admission, run, or promotion identities.</summary>
/// <remarks>The attempted input was not accepted into durable pending state. Callers can inspect <see cref="Rejection"/> without inferring that a queue record exists.</remarks>
public sealed record RejectedInput: InputAdmissionResult
{
    /// <summary>Initializes a pre-admission rejection result.</summary>
    /// <param name="rejection">The non-null typed content-free explanation of why admission did not occur.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public RejectedInput(InputRejection rejection) { ArgumentNullException.ThrowIfNull(rejection); Rejection = rejection; }
    /// <summary>Gets the typed failure that prevented durable admission.</summary>
    /// <value>A non-null content-free rejection; it is not evidence of a persisted queue record.</value>
    public InputRejection Rejection { get; }
}
