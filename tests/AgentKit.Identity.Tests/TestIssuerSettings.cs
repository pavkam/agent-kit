// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed record TestIssuerSettings(
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset? ExpiresAt,
    bool Revoked = false,
    long Version = 1,
    bool NullNormalization = false,
    bool RejectNormalization = false);
