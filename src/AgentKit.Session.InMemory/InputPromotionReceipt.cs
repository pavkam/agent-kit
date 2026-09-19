// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Retains one successful atomic mid-run input promotion for exact idempotency reconciliation.</summary>
internal sealed record InputPromotionReceipt(SessionInputPromotionRequest Request, SessionInputPromoted Result);
