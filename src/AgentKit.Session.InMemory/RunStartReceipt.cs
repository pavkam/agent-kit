// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Retains one successful atomic start for exact idempotency reconciliation.</summary>
internal sealed record RunStartReceipt(SessionRunStartRequest Request, SessionRunAccepted Result);
