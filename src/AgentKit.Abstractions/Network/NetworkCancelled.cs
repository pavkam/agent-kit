// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The operation was cancelled by the caller before it completed.</summary>
public sealed record NetworkCancelled: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkCancelled"/> record.</summary>
    /// <param name="sideEffectCertain">
    /// <see langword="true"/> when the transport can prove the request was
    /// never observably sent; <see langword="false"/> when whether it was
    /// sent is uncertain.
    /// </param>
    public NetworkCancelled(bool sideEffectCertain) => SideEffectCertain = sideEffectCertain;

    /// <summary>
    /// Gets a value indicating whether the transport can prove the request
    /// was never observably sent.
    /// </summary>
    public bool SideEffectCertain { get; init; }
}
