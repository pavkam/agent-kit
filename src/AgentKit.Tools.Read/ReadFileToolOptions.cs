// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>Configures the model-facing line window bounds for <see cref="ReadFileTool"/>.</summary>
/// <remarks>
/// <para>
/// An omitted <c>limit</c> argument never means "unbounded": the tool returns at most
/// <see cref="DefaultMaximumLines"/> logical lines starting at the requested offset. An
/// explicit <c>limit</c> may raise that window up to <see cref="MaximumLines"/>; larger
/// requests are rejected as invalid arguments before authorization or any file-system read.
/// </para>
/// <para>
/// Both bounds must be positive and <see cref="DefaultMaximumLines"/> must not exceed
/// <see cref="MaximumLines"/>. <c>AddReadTool</c> validates these rules through the options
/// pipeline and <see cref="ReadFileTool"/> re-validates them at construction, so an invalid
/// configuration fails at composition rather than in the middle of an agent run. These bounds
/// limit returned lines only; byte and encoding limits belong to the selected
/// <see cref="IFileSystem"/> boundary.
/// </para>
/// </remarks>
public sealed class ReadFileToolOptions
{
    /// <summary>Gets or sets the number of lines returned when the model omits <c>limit</c>.</summary>
    /// <value>
    /// A positive line count no greater than <see cref="MaximumLines"/>. The default of
    /// <c>2,000</c> follows the coding-harness sizing profile, which retains the head of a file read.
    /// </value>
    public int DefaultMaximumLines { get; set; } = 2_000;

    /// <summary>Gets or sets the largest <c>limit</c> the tool permits a model to request.</summary>
    /// <value>
    /// A positive ceiling on the returned-line window. Requests above this value are rejected with
    /// a typed invalid-arguments outcome that quotes the ceiling. The default is <c>20,000</c>.
    /// </value>
    public int MaximumLines { get; set; } = 20_000;
}
