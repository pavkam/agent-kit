// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Verifies <see cref="ToolLeafLogEvents"/> validation and delegate ownership.</summary>
public sealed class ToolLeafLogEventsTests
{
    private static readonly Action<ILogger, ToolId, ToolCallId, string> _completed = static (_, _, _, _) => { };
    private static readonly Action<ILogger, ToolId, ToolCallId> _cancelled = static (_, _, _) => { };
    private static readonly Action<ILogger, ToolId, ToolCallId, string> _faulted = static (_, _, _, _) => { };

    [Fact]
    public void Constructor_WhenADelegateIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => new ToolLeafLogEvents(null!, _cancelled, _faulted)).ParamName.ShouldBe("completed");
        Should.Throw<ArgumentNullException>(() => new ToolLeafLogEvents(_completed, null!, _faulted)).ParamName.ShouldBe("cancelled");
        Should.Throw<ArgumentNullException>(() => new ToolLeafLogEvents(_completed, _cancelled, null!)).ParamName.ShouldBe("faulted");
    }

    [Fact]
    public void Constructor_WhenDelegatesAreProvided_ExposesThem()
    {
        var events = new ToolLeafLogEvents(_completed, _cancelled, _faulted);

        events.Completed.ShouldBeSameAs(_completed);
        events.Cancelled.ShouldBeSameAs(_cancelled);
        events.Faulted.ShouldBeSameAs(_faulted);
    }
}
