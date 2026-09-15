// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares persistence guarantees provided by an approval store.</summary>
/// <param name="IsDurable">Whether requests and resolutions survive process loss.</param>
/// <param name="ProvidesTrustedControlPlane">Whether access is restricted to the trusted approval control plane.</param>
public readonly record struct ApprovalStoreCapabilities(
    bool IsDurable,
    bool ProvidesTrustedControlPlane = false);
