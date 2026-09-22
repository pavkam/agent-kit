// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;

internal static class TestSearchComposition
{
    internal static SearchTool CreateTool(IFileContentSearcher searcher, ISecurityAuthority authority)
    {
        var profileKey = new FileSystemProfileKey("test");
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(profileKey.Value, searcher);
        var provider = services.BuildServiceProvider();
        return new SearchTool(
            provider,
            new FixedSecurityAuthoritySelector(authority),
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new SearchToolOptions { ProfileKey = profileKey }));
    }
}
