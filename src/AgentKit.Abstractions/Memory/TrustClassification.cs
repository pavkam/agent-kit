// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies how much trust retrieval and context assembly may place in a candidate.</summary>
public enum TrustClassification
{
    /// <summary>Content originated from an authoritative host or verified store write.</summary>
    Authoritative,

    /// <summary>Content was retrieved from durable storage with intact provenance.</summary>
    Stored,

    /// <summary>Content is retrieved or generated data that must remain untrusted in model context.</summary>
    UntrustedData,
}
