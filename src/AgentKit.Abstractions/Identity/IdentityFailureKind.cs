// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies identity-resolution failures without confusing authentication with authorization.</summary>
public enum IdentityFailureKind
{
    /// <summary>The assertion or request is malformed.</summary>
    Malformed,
    /// <summary>No configured issuer matches the assertion.</summary>
    UnknownIssuer,
    /// <summary>The issuer-to-tenant or subject mapping is ambiguous.</summary>
    AmbiguousMapping,
    /// <summary>The authentication evidence has expired.</summary>
    Expired,
    /// <summary>The authentication evidence has been revoked.</summary>
    Revoked,
    /// <summary>The issuer, subject, authentication method, or claim shape is unsupported.</summary>
    Unsupported,
    /// <summary>The established identity does not meet required assurance.</summary>
    InsufficientAssurance,
    /// <summary>A delegated identity would broaden or erase its parent's identity constraints.</summary>
    DelegationWouldBroaden,
    /// <summary>A required identity service failed without producing a more specific rejection.</summary>
    Unavailable,
}
