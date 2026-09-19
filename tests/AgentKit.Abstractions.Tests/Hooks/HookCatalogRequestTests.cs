// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookCatalogRequestTests
{
    [Fact]
    public void Constructor_WhenProfileKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookCatalogRequest(default));
        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsProfileKey() =>
        new HookCatalogRequest(HookKernelTestData.Profile).ProfileKey.ShouldBe(HookKernelTestData.Profile);
}
