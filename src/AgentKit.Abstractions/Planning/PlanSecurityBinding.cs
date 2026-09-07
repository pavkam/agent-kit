// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;

/// <summary>Produces canonical resources and fingerprints for current-plan operations.</summary>
public static class PlanSecurityBinding
{
    /// <summary>Creates the exact application-state resource for one session's current plan.</summary>
    /// <param name="address">The complete session address.</param>
    /// <returns>The canonical application-state resource.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public static ProtectedResource Resource(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"session:{address.AgentId}/{address.SessionId}/plan");
    }

    /// <summary>Fingerprints an exact current-plan read.</summary>
    /// <param name="address">The target session.</param>
    /// <returns>The deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public static InputFingerprint ReadFingerprint(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Fingerprint(new
        {
            action = "read",
            agent = address.AgentId.ToString(),
            session = address.SessionId.ToString(),
        });
    }

    /// <summary>Fingerprints an exact optimistic plan replacement.</summary>
    /// <param name="address">The target session.</param>
    /// <param name="title">The exact title.</param>
    /// <param name="items">The exact ordered items.</param>
    /// <param name="expectedRevision">The expected current revision, or null.</param>
    /// <returns>The deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is blank or <paramref name="items"/> is default.</exception>
    public static InputFingerprint ReplaceFingerprint(
        SessionAddress address,
        string title,
        ImmutableArray<WorkPlanItem> items,
        PlanRevision? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfContainsNull(items);
        return Fingerprint(new
        {
            action = "replace",
            agent = address.AgentId.ToString(),
            session = address.SessionId.ToString(),
            title,
            items = items.Select(static item => new { id = item.Id.Value, item.Text, status = item.Status.ToString() }),
            expectedRevision = expectedRevision?.Value,
        });
    }

    /// <summary>Fingerprints an exact optimistic item-status transition.</summary>
    /// <param name="address">The target session.</param>
    /// <param name="itemId">The exact item.</param>
    /// <param name="status">The requested status.</param>
    /// <param name="expectedRevision">The expected current revision.</param>
    /// <returns>The deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    public static InputFingerprint StatusFingerprint(
        SessionAddress address,
        PlanItemId itemId,
        PlanItemStatus status,
        PlanRevision expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        return Fingerprint(new
        {
            action = "set-status",
            agent = address.AgentId.ToString(),
            session = address.SessionId.ToString(),
            itemId = itemId.Value,
            status = status.ToString(),
            expectedRevision = expectedRevision.Value,
        });
    }

    private static InputFingerprint Fingerprint<T>(T value) =>
        new(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))));
}
