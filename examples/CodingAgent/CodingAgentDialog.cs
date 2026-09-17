// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using SharpVision.Dialogs;

/// <summary>The base of every CodingAgent dialog: a centered, non-movable modal window whose
/// content is one body above the framework's separator-and-buttons action bar.</summary>
/// <typeparam name="TResult">The typed value the dialog completes with.</typeparam>
/// <remarks>
/// <see cref="Dialog{TResult}.CreateActionBar"/> is protected, so the one place that knows how a
/// CodingAgent dialog is laid out has to be a dialog itself. Every concrete dialog composes its
/// body and buttons through <see cref="Compose"/>, which keeps padding, the separator, button
/// spacing, and the optional dim hint identical across the application.
/// </remarks>
internal abstract class CodingAgentDialog<TResult>: Dialog<TResult>
{
    /// <summary>Initializes the shared chrome for a CodingAgent dialog.</summary>
    /// <param name="title">The non-empty header title, shown centered with one cell of breathing room.</param>
    /// <param name="cancelledResult">The value a dismissed dialog completes with.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null or whitespace.</exception>
    protected CodingAgentDialog(string title, TResult cancelledResult)
        : base(cancelledResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Header = $" {title} ";
        CanResize = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Wraps one group of related controls in a titled frame so every dialog sections its
    /// content the same way.</summary>
    /// <param name="title">The non-empty plain section title written into the frame's top edge.</param>
    /// <param name="content">The non-null content arranged inside the frame with one cell of horizontal breathing room.</param>
    /// <returns>A stretched group box with one row of margin below it.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    protected static GroupBox Section(string title, ControlBase content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new GroupBox
        {
            HeaderText = title,
            Content = content,
            Padding = new Thickness(1, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    /// <summary>Creates one explanatory line that wraps to the available width instead of clipping.</summary>
    /// <param name="markup">The non-null text markup.</param>
    /// <returns>A wrapping, stretched text control.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markup"/> is null.</exception>
    protected static Text Note(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new Text(markup)
        {
            Overflow = Overflow.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    /// <summary>Builds the dialog content: <paramref name="body"/> above a separator and the
    /// action buttons, with an optional dim keyboard hint on the action row's left.</summary>
    /// <param name="body">The non-null body control; it receives the remaining height when
    /// <paramref name="bodyFills"/> is set, otherwise its own desired height.</param>
    /// <param name="buttons">The non-empty buttons in left-to-right order.</param>
    /// <param name="hint">Optional plain hint text; null shows none and centers the buttons.</param>
    /// <param name="bodyFills">Whether the body stretches to the dialog's fixed height.</param>
    /// <returns>The grid to assign to the window content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="buttons"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="buttons"/> is empty.</exception>
    protected static Grid Compose(ControlBase body, Button[] buttons, string? hint = null, bool bodyFills = false)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentOutOfRangeException.ThrowIfZero(buttons.Length);

        var buttonRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = hint is null ? HorizontalAlignment.Center : HorizontalAlignment.Right,
        };
        foreach (var button in buttons)
        {
            buttonRow.Children.Add(button);
        }

        // Buttons sit centered, the way MessageBox lays its own out, unless a hint occupies the
        // left of the row - then the hint takes the stretch column and the buttons keep right.
        var actions = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        actions.Columns.Add(Track.Star(1));
        actions.Columns.Add(Track.Auto());
        if (hint is null)
        {
            actions.Columns.Add(Track.Star(1));
        }
        else
        {
            var hintText = new Text($"<d>{Text.Escape(hint)}</d>") { VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(hintText);
        }

        Grid.SetColumn(buttonRow, 1);
        actions.Children.Add(buttonRow);

        var actionBar = CreateActionBar(actions, buttons, out _);
        body.HorizontalAlignment = HorizontalAlignment.Stretch;
        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(1, 0),
        };
        content.Columns.Add(Track.Star(1));
        content.Rows.Add(bodyFills ? Track.Star(1) : Track.Auto());
        content.Rows.Add(Track.Auto());
        Grid.SetRow(actionBar, 1);
        content.Children.Add(body);
        content.Children.Add(actionBar);
        return content;
    }
}
