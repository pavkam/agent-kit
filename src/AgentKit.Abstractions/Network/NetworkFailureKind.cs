// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The portable, normalized category of one failed network operation.</summary>
public enum NetworkFailureKind
{
    /// <summary>DNS resolution did not produce a usable address.</summary>
    DnsResolutionFailed,

    /// <summary>A connection to a resolved address could not be established.</summary>
    ConnectionFailed,

    /// <summary>TLS negotiation failed.</summary>
    TlsFailure,

    /// <summary>The operation did not complete before its deadline.</summary>
    Timeout,

    /// <summary>The requested scheme is not supported.</summary>
    UnsupportedScheme,

    /// <summary>The remote peer violated the expected wire protocol.</summary>
    ProtocolViolation,

    /// <summary>The operation was cancelled by the caller before it completed.</summary>
    Cancelled,

    /// <summary>The failure does not fit any other defined category.</summary>
    Unknown
}
