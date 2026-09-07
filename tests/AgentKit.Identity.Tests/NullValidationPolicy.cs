// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class NullValidationPolicy: IIdentityValidationPolicy { public ValueTask<IdentityValidationResult> ValidateAsync(ExecutionIdentity identity, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityValidationResult>(null!); }
