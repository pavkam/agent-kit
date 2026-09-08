// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the dispatcher accepted an audit record under its configured delivery policy.</summary>
public sealed record SecurityAuditAccepted: SecurityAuditDispatchResult;
