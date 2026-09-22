// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;

internal static class TestGlobComposition
{
    internal static GlobTool CreateTool(IFileGlobber globber, ISecurityAuthority authority)
    {
        var profileKey = new FileSystemProfileKey("test");
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(profileKey.Value, globber);
        var provider = services.BuildServiceProvider();
        return new GlobTool(
            provider,
            new FixedSecurityAuthoritySelector(authority),
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new GlobToolOptions { ProfileKey = profileKey }));
    }
}
