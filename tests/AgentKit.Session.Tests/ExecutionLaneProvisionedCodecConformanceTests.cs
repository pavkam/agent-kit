// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ExecutionLaneProvisionedCodecConformanceTests:
    SessionEntryCodecConformanceTests<ExecutionLaneProvisionedCodecConformanceTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new ExecutionLaneProvisionedSessionEntryCodec(
            TimeProvider.System,
            NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance);

        public SessionEntry CreateEntry() => PortableSessionEntryCodecTestEntries.Lane();

        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) => expected == actual;
    }
}
