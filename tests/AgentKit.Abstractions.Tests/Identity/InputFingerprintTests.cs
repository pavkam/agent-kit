// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class InputFingerprintTests: Conformance.StringIdentityConformanceTests<InputFingerprint>
{

    /// <inheritdoc/>
    protected override InputFingerprint Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(InputFingerprint subject) => subject.Value;
}
