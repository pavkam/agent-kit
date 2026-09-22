// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;

internal static class TestPatchComposition
{
    internal static PatchTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IWorkspacePatchApplier applier,
        ISecurityAuthority authority,
        PatchToolOptions? options = null)
    {
        var configured = options ?? new PatchToolOptions();
        var profileKey = configured.ProfileKey;
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(profileKey.Value, snapshotReader);
        _ = services.AddKeyedSingleton(profileKey.Value, applier);
        var provider = services.BuildServiceProvider();
        return new PatchTool(
            provider,
            new FixedSecurityAuthoritySelector(authority),
            new SequenceSecurityRequestIdGenerator(),
            new SequenceMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(configured));
    }
}
