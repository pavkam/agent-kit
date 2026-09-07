// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class ReplacingVersionPolicy: IIdentityNormalizationPolicy { public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(new ExecutionIdentity(request.Candidate.TenantId, request.Candidate.PrincipalId, request.Candidate.SubjectKind, request.Candidate.Evidence, request.Candidate.Claims, request.Candidate.DelegationChain, request.Candidate.Assurance, new IdentityVersion(request.Candidate.Version.Value + 1)))); }
