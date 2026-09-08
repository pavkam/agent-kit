// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no authorized admission uses the requested input identity.</summary>
public sealed record SessionInputNotFound: SessionInputLookupResult;
