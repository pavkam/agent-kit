// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that observation content was omitted because capture was disabled or redaction failed closed.</summary>
public sealed record ContentOmitted: RedactionResult;
