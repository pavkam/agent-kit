// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies how a responder authenticated to a trusted approval channel before answering.</summary>
/// <remarks>This vocabulary names channel-level authentication evidence only; it never itself authorizes the operation being approved. See <c>docs/architecture/permissions-and-human-control.md</c> for the interim-type note that introduces this enumeration.</remarks>
public enum ApprovalAuthenticationMethod
{
    /// <summary>A password or other shared secret verified by the channel.</summary>
    Password,
    /// <summary>A single-use time- or event-based passcode.</summary>
    OneTimePasscode,
    /// <summary>A hardware security key or platform authenticator assertion.</summary>
    HardwareSecurityKey,
    /// <summary>A federated single-sign-on assertion from a trusted issuer.</summary>
    SingleSignOnAssertion,
    /// <summary>A long-lived API credential presented by an automated responder.</summary>
    ApiCredential,
}
