// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares TLS validation behavior for one network profile.</summary>
public enum NetworkTlsPolicy
{
    /// <summary>Use platform default certificate validation and SNI for the canonical host.</summary>
    PlatformDefault = 0,

    /// <summary>Pin one expected server certificate fingerprint in addition to platform validation.</summary>
    PinCertificateFingerprint = 1,
}
