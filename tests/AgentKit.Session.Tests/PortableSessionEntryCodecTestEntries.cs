// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

internal static class PortableSessionEntryCodecTestEntries
{
    private static readonly SchemaVersion _version = new("1");

    internal static ExecutionLaneProvisionedSessionEntry Lane() => new(
        new SessionEntryId(Id(1)),
        new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))),
        new BeforeRunOperationCorrelation(new OperationId(Id(4)), null),
        new BranchId(Id(5)),
        new SessionSequence(1),
        null,
        DateTimeOffset.UnixEpoch,
        _version,
        new ExecutionLaneId(Id(6)),
        new SessionLaneRevision(1),
        new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1)),
        new RunConfigurationReference(
            new ConfigurationVersion(1),
            new RunPolicyVersion(1),
            new ContentHash("sha256:test")));

    internal static InputPromotedSessionEntry Promotion() => new(
        new SessionEntryId(Id(11)),
        new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))),
        new InRunOperationCorrelation(new OperationId(Id(4)), new RunId(Id(7)), new TurnId(Id(8))),
        new BranchId(Id(5)),
        new SessionSequence(2),
        new SessionEntryId(Id(1)),
        DateTimeOffset.UnixEpoch,
        _version,
        new ExecutionLaneId(Id(6)),
        new AdmissionId(Id(9)),
        new SessionSequence(1),
        [new AdmissionId(Id(9)), new AdmissionId(Id(10))]);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
