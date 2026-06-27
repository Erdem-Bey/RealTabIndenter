using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;		// I disabled this because it cannot find "Composition" class or namespace. This library is not used anyway.
//using System.Composition;

namespace RealTabIndenter
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("CSharp")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	internal sealed class RealIndentProvider : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView)
		{
			System.IO.File.AppendAllText(@"D:\vsix_debug.txt", "listener attached\n");	// I changed drive to D: because I want to avoid writes to my SSD drive C as much as possible.
			_ = new RealIndentInserter(textView);
		}
	}

	internal sealed class RealIndentInserter
	{
		private readonly IWpfTextView _view;
		private bool _busy;
		private EventHandler<CaretPositionChangedEventArgs> _pendingCaretFix;

		public RealIndentInserter(IWpfTextView view)
		{
			_view = view;
			view.TextBuffer.Changed += OnChanged;
		}

		private void OnChanged(object sender, TextContentChangedEventArgs e)
		{
			if (_busy) return;

			foreach (var change in e.Changes)
			{
				if (change.OldLength != 0) continue;
				if (change.NewText != "\r\n" && change.NewText != "\n") continue;

				var snapshot = e.After;
				int newLineStart = change.NewPosition + change.NewLength;
				if (newLineStart >= snapshot.Length) continue;

				var line = new SnapshotPoint(snapshot, newLineStart).GetContainingLine();

				// If Roslyn already wrote real characters, do nothing
				if (line.Length != 0) continue;

				// Read tab count from the nearest non-empty line above
				int tabCount = GetTabCountFromContext(snapshot, line.LineNumber);
				if (tabCount <= 0) continue;

				_busy = true;
				string tabs = new('\t', tabCount);
				int lineNum = line.LineNumber;

				_busy = true;
				try
				{
					using var edit = _view.TextBuffer.CreateEdit();
					edit.Insert(line.Start, tabs);
					edit.Apply();
				}
				finally
				{
					_busy = false;
				}

				// Remove any leftover handler from a rapid previous Enter press
				if (_pendingCaretFix != null)
				{
					_view.Caret.PositionChanged -= _pendingCaretFix;
					_pendingCaretFix = null;
				}

				// Subscribe AFTER the edit so the edit-induced caret nudge (VirtualSpaces == 0)
				// doesn't trigger us. We only want to catch Roslyn's subsequent virtual-space move.
				// Caret.PositionChanged fires synchronously — before any frame renders — so
				// correcting here eliminates both the visual flash and the typing-into-gap problem.
				_pendingCaretFix = (_, args) =>
				{
					_view.Caret.PositionChanged -= _pendingCaretFix;
					_pendingCaretFix = null;
					if (!_view.IsClosed && args.NewPosition.VirtualSpaces > 0)
					{
						var correctedLine = _view.TextSnapshot.GetLineFromLineNumber(lineNum);
						_view.Caret.MoveTo(correctedLine.End);
					}
				};
				_view.Caret.PositionChanged += _pendingCaretFix;
				break;
			}
		}

		private static int GetTabCountFromContext(ITextSnapshot snapshot, int blankLineNumber)
		{
			// Walk upward to find the nearest non-empty line and count its leading tabs
			for (int i = blankLineNumber - 1; i >= 0; i--)
			{
				var above = snapshot.GetLineFromLineNumber(i);
				string text = above.GetText();
				if (string.IsNullOrWhiteSpace(text)) continue;

				int tabs = 0;
				foreach (char c in text)
				{
					if (c == '\t') tabs++;
					else break;
				}
				return tabs;
			}
			return 0;
		}
	}
}