// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>No network profile is registered for the requested key.</summary>
/// <param name="Key">The missing profile key.</param>
public sealed record NetworkProfileMissing(NetworkProfileKey Key): NetworkProfileSelectionResult;
