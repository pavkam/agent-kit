// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

internal sealed class RecordingProcessOutputArtifactSink: IProcessOutputArtifactSink
{
    internal List<ProcessOutputArtifactRequest> Requests { get; } = [];
    internal ArtifactReference Reference { get; } = new(
        new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ArtifactVersion("1"),
        new ArtifactDirectoryId("process-output"),
        new ArtifactProfileKey("test"),
        new ArtifactProfileVersion(1),
        new TenantId("tenant"),
        new ArtifactOwnerId("session:owner"),
        new PrincipalId("principal"),
        "application/octet-stream",
        10,
        new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch),
        ArtifactDataClassification.Internal,
        ArtifactOwnershipKind.Session,
        ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false),
        DateTimeOffset.UnixEpoch);

    public Task<ProcessOutputArtifactResult> StoreAsync(ProcessOutputArtifactRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult<ProcessOutputArtifactResult>(new ProcessOutputArtifactStored(Reference));
    }
}
