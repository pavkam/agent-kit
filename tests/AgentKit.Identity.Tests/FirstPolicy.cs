// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class FirstPolicy(PolicyTrace trace): IIdentityNormalizationPolicy { public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default) { trace.Entries.Add("first"); return ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(request.Candidate)); } }
