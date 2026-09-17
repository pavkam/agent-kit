// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Keys one idempotent attempt at a named store operation within an authenticated tenant partition.</summary>
/// <remarks>Used to detect a replayed caller attempt so an equivalent retry returns the original receipt and a
/// conflicting retry is rejected, independent of any resource identity the caller supplied on the retry.</remarks>
internal readonly record struct ReplayKey
{
    /// <summary>Initializes a tenant-qualified idempotent-attempt key.</summary>
    /// <param name="tenantId">The non-empty authenticated tenant that owns the attempt.</param>
    /// <param name="operation">The non-empty stable name of the store operation being replayed.</param>
    /// <param name="key">The non-empty caller-supplied idempotency key value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenantId"/>, <paramref name="operation"/>, or <paramref name="key"/> has a null value.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/>, <paramref name="operation"/>, or <paramref name="key"/> has an empty or whitespace value.</exception>
    internal ReplayKey(TenantId tenantId, string operation, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        TenantId = tenantId;
        Operation = operation;
        Key = key;
    }

    /// <summary>Gets the authenticated tenant that owns the attempt.</summary>
    /// <value>The validated tenant, or the default tenant only on a zero-initialized, invalid default key.</value>
    internal TenantId TenantId { get; }

    /// <summary>Gets the stable name of the store operation being replayed.</summary>
    /// <value>The validated non-empty operation name, or <see langword="null"/> only on a zero-initialized, invalid default key.</value>
    internal string Operation { get; }

    /// <summary>Gets the caller-supplied idempotency key value.</summary>
    /// <value>The validated non-empty key value, or <see langword="null"/> only on a zero-initialized, invalid default key.</value>
    internal string Key { get; }
}
