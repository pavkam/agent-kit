// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using SharpVision.Controls.Document;
using SharpVision.Documents.Markdown;

/// <summary>A read-only dialog that presents one authored Markdown reference in a selectable,
/// scrollable <see cref="Document"/> above the standard dialog action bar.</summary>
/// <remarks>
/// One dialog type serves the keyboard, command, and tool references: they differ only in title
/// and content, so each presentation builds a fresh instance from its content provider and the
/// framework disposes it on close.
/// </remarks>
internal sealed class MarkdownReferenceDialog: CodingAgentDialog<bool>
{
    /// <summary>Initializes a reference dialog for one Markdown text.</summary>
    /// <param name="title">The non-empty header title.</param>
    /// <param name="markdown">The non-null authored Markdown.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is null.</exception>
    public MarkdownReferenceDialog(string title, string markdown)
        : base(title, cancelledResult: false)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        Width = Length.Percent(88);
        MaxWidth = Length.Cells(86);
        Height = Length.Percent(84);
        MaxHeight = Length.Cells(34);

        Document = new Document
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTextSelectionEnabled = true,
            IsFocusable = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };
        _ = Document.Load(markdown, new MarkdownDocumentReader());

        var close = new Button("&Close") { IsDefault = true, IsCancel = true };
        close.Click += (_, _) => Complete(true);
        Content = Compose(Document, [close], hint: "↑/↓ scroll · Ctrl+A select all", bodyFills: true);
    }

    /// <summary>Gets the semantic Markdown document, for focused verification.</summary>
    internal Document Document { get; }

    /// <summary>Presents a fresh reference dialog and completes when it closes.</summary>
    /// <param name="owner">The attached control whose presentation host shows the dialog.</param>
    /// <param name="title">The non-empty header title.</param>
    /// <param name="markdown">The non-null authored Markdown.</param>
    /// <param name="cancellationToken">Cancels the presentation, closing the dialog.</param>
    /// <returns>A task that completes after the dialog closes.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is detached or <paramref name="title"/> is blank.</exception>
    public static Task ShowAsync(ControlBase owner, string title, string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new MarkdownReferenceDialog(title, markdown);
        return dialog.PresentAsync(owner, dialog.Document, cancellationToken);
    }
}
