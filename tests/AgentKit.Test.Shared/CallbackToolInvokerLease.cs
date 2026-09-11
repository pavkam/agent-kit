// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Supplies typed lease metadata and observable release for catalog boundary tests.</summary>
/// <param name="tool">The deliberately controllable descriptor.</param><param name="sourceVersion">The source version returned by the fake.</param><param name="invoker">The borrowed invoker returned by the fake.</param>
public sealed class CallbackToolInvokerLease(ToolDescriptor tool, ToolSourceVersion sourceVersion, IToolInvoker invoker): IToolInvokerLease
{
    private int _disposals;
    /// <summary>Gets or sets the optional release callback.</summary>
    public Func<ValueTask>? Release { get; set; }
    /// <summary>Gets or sets a controlled descriptor getter override.</summary>
    public Func<ToolDescriptor>? ReadTool { get; set; }
    /// <summary>Gets or sets a controlled invoker getter override.</summary>
    public Func<IToolInvoker>? ReadInvoker { get; set; }
    /// <summary>Gets the number of observed release calls.</summary>
    public int Disposals => Volatile.Read(ref _disposals);
    /// <inheritdoc/>
    public ToolDescriptor Tool => ReadTool is null ? tool : ReadTool();
    /// <inheritdoc/>
    public ToolSourceVersion SourceVersion => sourceVersion;
    /// <inheritdoc/>
    public IToolInvoker Invoker => ReadInvoker is null ? invoker : ReadInvoker();
    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposals);
        return Release?.Invoke() ?? ValueTask.CompletedTask;
    }
}
