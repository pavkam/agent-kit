// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;



/// <summary>Verifies BoundedWriteStream behavior and contracts.</summary>
public sealed class BoundedWriteStreamTests
{
    /// <summary>Proves stream caller guards run before the configured payload bound.</summary>
    [Fact]
    public void BoundedWriteStream_WhenArgumentsAreInvalid_UsesExactCallerParameter()
    {
        using var stream = new BoundedWriteStream(1);
        Should.Throw<ArgumentNullException>(() => stream.Write(null!, 0, 0)).ParamName.ShouldBe("buffer");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write([], -1, 0)).ParamName.ShouldBe("offset");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write([], 0, -1)).ParamName.ShouldBe("count");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.SetLength(2)).ParamName.ShouldBe("value");
    }

    /// <summary>Proves repeated small writes grow geometrically without allocating beyond the configured envelope.</summary>
    [Fact]
    public void BoundedWriteStream_WhenManySmallFieldsAreWritten_GrowsWithinBound()
    {
        using var stream = new BoundedWriteStream(4096);
        for (var index = 0; index < 1024; index++)
        {
            stream.WriteByte(1);
        }

        stream.Length.ShouldBe(1024);
        stream.Capacity.ShouldBeInRange(1024, 4096);
    }
}
