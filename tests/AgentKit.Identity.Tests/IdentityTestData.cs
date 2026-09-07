// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal static class IdentityTestData
{
    internal static AuthenticationEvidence Evidence(string issuer, DateTimeOffset authenticatedAt, DateTimeOffset? expiresAt) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "test", authenticatedAt, expiresAt, new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
    internal static IdentityClaim Claim(string issuer, string type, string value) => new(new IdentityIssuerId(issuer), type, value, IdentityClaimValueKind.Text);
}
