// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionSessionEntry behavior and contracts.</summary>
public sealed class CompactionSessionEntryTests
{
    [Fact]
    public void CompactionSessionEntry_Constructor_WhenRecordNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionSessionEntry(new SessionEntryId(Guid.NewGuid()), new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), Correlation(), new BranchId(Guid.NewGuid()), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), null!));
        exception.ParamName.ShouldBe("record");
    }

    private static readonly Guid _fixedOperationGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid _fixedRunGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_fixedOperationGuid), new RunId(_fixedRunGuid), null);
}
