// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using System.Text.Json;

using AgentKit.TestSupport;

public sealed class DeferredRequestIdTests
{
    [Fact]
    public void Constructor_WhenDeferredIdentityIsEmpty_RejectsAndPreservesValidJsonRoundTrip()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DeferredRequestId(Guid.Empty)).ParamName.ShouldBe("value");
        var id = RunResultTestData.Deferred().Id;
        JsonSerializer.Deserialize<DeferredRequestId>(JsonSerializer.Serialize(id)).ShouldBe(id);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<DeferredRequestId>("{\"Value\":\"00000000-0000-0000-0000-000000000000\"}"));
    }
}
