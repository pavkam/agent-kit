// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class InputPromotedCodecConformanceTests:
    SessionEntryCodecConformanceTests<InputPromotedCodecConformanceTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new InputPromotedSessionEntryCodec(
            TimeProvider.System,
            NullLogger<InputPromotedSessionEntryCodec>.Instance);

        public SessionEntry CreateEntry() => PortableSessionEntryCodecTestEntries.Promotion();

        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual)
        {
            var left = (InputPromotedSessionEntry) expected;
            var right = (InputPromotedSessionEntry) actual;
            return left.Id == right.Id && left.Address == right.Address && left.Correlation == right.Correlation
                && left.BranchId == right.BranchId && left.Sequence == right.Sequence && left.CausalParentId == right.CausalParentId
                && left.RecordedAt == right.RecordedAt && left.SchemaVersion == right.SchemaVersion
                && left.ExecutionLaneId == right.ExecutionLaneId && left.InitiatingAdmissionId == right.InitiatingAdmissionId
                && left.Cutoff == right.Cutoff && left.AdmissionIds.SequenceEqual(right.AdmissionIds);
        }
    }
}
