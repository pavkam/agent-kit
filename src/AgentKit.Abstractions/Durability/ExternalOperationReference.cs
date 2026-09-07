// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A durable handle to work accepted by an external owner, such as a workflow
/// engine or a provider's server-side operation, used to resume waiting or to
/// query the true outcome after process loss.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The reference binds the remote handle to the system that issued it. A
/// bare handle string is not sufficient evidence: the same identifier means
/// different things to different backends and accounts, and resuming against
/// the wrong owner is how recovery silently reports another tenant's result.
/// </para>
/// <para>
/// A process-local delay, poll loop, or in-memory task is never a substitute
/// for this reference. If no external reference was recorded, there is no
/// proof that any external owner accepted the work.
/// </para>
/// </remarks>
public sealed record ExternalOperationReference
{
    private readonly string _handle;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ExternalOperationReference"/> record.
    /// </summary>
    /// <param name="backendKey">
    /// The backend that issued and understands the handle.
    /// </param>
    /// <param name="handle">
    /// The non-empty opaque handle assigned by the external owner.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="handle"/> is null, empty, or consists only of
    /// whitespace. An empty handle is not a valid acceptance record.
    /// </exception>
    public ExternalOperationReference(DurableBackendKey backendKey, string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        BackendKey = backendKey;
        _handle = handle;
    }

    /// <summary>Gets the backend that issued the handle.</summary>
    public DurableBackendKey BackendKey { get; init; }

    /// <summary>Gets the opaque handle assigned by the external owner.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Handle
    {
        get => _handle;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Handle));
            _handle = value;
        }
    }
}
