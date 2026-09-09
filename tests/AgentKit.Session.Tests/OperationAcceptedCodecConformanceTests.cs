// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class OperationAcceptedCodecConformanceTests:
    SessionEntryCodecConformanceTests<OperationAcceptedCodecConformanceTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new OperationAcceptedSessionEntryCodec(
            TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);

        public SessionEntry CreateEntry() => OperationAcceptedSessionEntryCodecTestData.Entry();

        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) =>
            OperationAcceptedSessionEntryCodecTestData.Equivalent(
                (OperationAcceptedSessionEntry) expected, (OperationAcceptedSessionEntry) actual);
    }
}
