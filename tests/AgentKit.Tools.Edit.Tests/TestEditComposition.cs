// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;

internal static class TestEditComposition
{
    internal static EditTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IAtomicFileReplacer replacer,
        ISecurityAuthority authority) =>
        CreateTool(snapshotReader, replacer, authority, new EditToolOptions());

    internal static EditTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IAtomicFileReplacer replacer,
        ISecurityAuthority authority,
        EditToolOptions options)
    {
        var profileKey = options.ProfileKey;
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(profileKey.Value, snapshotReader);
        _ = services.AddKeyedSingleton(profileKey.Value, replacer);
        var provider = services.BuildServiceProvider();
        return new EditTool(
            provider,
            new FixedSecurityAuthoritySelector(authority),
            new SequenceSecurityRequestIdGenerator(),
            new StubMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options));
    }
}
