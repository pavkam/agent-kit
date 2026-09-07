// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal holds no record at all for the requested operation address.
/// </summary>
/// <remarks>
/// <para>
/// Absence of a record is weaker evidence than it appears and is deliberately
/// a distinct outcome from
/// <see cref="RecoveryEvidenceLoaded"/> carrying
/// <see cref="RecoveryEvidence.StartDefinitelyAbsent"/>. This result says the
/// journal knows nothing; it does not by itself prove the effect never
/// happened, because the acceptance write may have been the thing that was
/// lost.
/// </para>
/// <para>
/// Treating "not found" as "safe to start" is only valid when the composition
/// guarantees acceptance is committed before any effect, which is exactly why
/// that ordering is mandatory.
/// </para>
/// </remarks>
public sealed record RecoveryEvidenceNotFound: RecoveryEvidenceResult
{
    private readonly DurableOperationAddress _address;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryEvidenceNotFound"/> record.
    /// </summary>
    /// <param name="address">The address that produced no record.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    public RecoveryEvidenceNotFound(DurableOperationAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _address = address;
    }

    /// <summary>Gets the address that produced no record.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableOperationAddress Address
    {
        get => _address;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Address));
            _address = value;
        }
    }
}
