// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>The immutable marker <c>AddSecurityAuthority</c> registers beside each binding to record its key.</summary>
/// <remarks>
/// Bindings are produced by factories that may activate the authority, so a validation-time catalog must not read them.
/// The marker is registered as an instance and carries only the key, letting <see cref="SecurityAuthorityCatalog"/> answer
/// without activating, invoking, or constructing any authority.
/// </remarks>
/// <param name="Key">The nonblank key the paired binding was installed under.</param>
internal sealed record SecurityAuthorityKeyRegistration(ComponentKey<ISecurityAuthority> Key);
