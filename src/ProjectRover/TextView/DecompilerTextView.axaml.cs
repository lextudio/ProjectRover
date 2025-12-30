// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Xml;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;

using AvaloniaEdit;

using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

using AvaloniaEdit.Folding;

using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

using AvaloniaEdit.Rendering;
using Avalonia.Input;
using Avalonia.Interactivity;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Controls;
using ICSharpCode.ILSpy.AssemblyTree;
using ICSharpCode.ILSpy.AvalonEdit;
using ICSharpCode.ILSpy.Options;
using ICSharpCode.ILSpy.Themes;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.ILSpy.ViewModels;
using ICSharpCode.ILSpyX;

using Microsoft.Win32;

using ICSharpCode.ILSpy.Views;

using TomsToolbox.Composition;
using TomsToolbox.Wpf;
using AvaloniaEdit.TextMate;
using TextMateSharp.Themes;
using TextMateSharp.Registry;

using ResourceKeys = ICSharpCode.ILSpy.Themes.ResourceKeys;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using System.Windows.Threading;
using TextMateSharp.Grammars;
using RegistryOptions = TextMateSharp.Grammars.RegistryOptions;
using Popup = ICSharpCode.ILSpy.Controls.Popup;

namespace ICSharpCode.ILSpy.TextView
{
	/// <summary>
	/// Manages the TextEditor showing the decompiled code.
	/// Contains all the threading logic that makes the decompiler work in the background.
	/// </summary>
	public sealed partial class DecompilerTextView : UserControl, IHaveState, IProgress<DecompilationProgress>
	{
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("DecompilerTextView");
		readonly IExportProvider exportProvider;
		readonly SettingsService settingsService;
		readonly LanguageService languageService;
        MainWindow MainWindowInstance => exportProvider.GetExportedValue<MainWindow>();
		readonly ReferenceElementGenerator referenceElementGenerator;
		readonly UIElementGenerator uiElementGenerator;
		readonly List<VisualLineElementGenerator?> activeCustomElementGenerators = new List<VisualLineElementGenerator?>();
		readonly BracketHighlightRenderer bracketHighlightRenderer;

		RichTextColorizer? activeRichTextColorizer;
		RichTextModel? activeRichTextModel;
		FoldingManager? foldingManager;
		ILSpyTreeNode[]? decompiledNodes;
		Uri? currentAddress;
		string? currentTitle;
		bool expandMemberDefinitions;

		DefinitionLookup? definitionLookup;
		TextSegmentCollection<ReferenceSegment>? references;
		CancellationTokenSource? currentCancellationTokenSource;

		readonly TextMarkerService textMarkerService;
		readonly List<ITextMarker> localReferenceMarks = new List<ITextMarker>();
		
		static ThemeName ResolveTextMateTheme(string? appThemeName)
		{
			if (!string.IsNullOrEmpty(appThemeName)
				&& appThemeName.Equals("dark", StringComparison.OrdinalIgnoreCase))
			{
				return ThemeName.AtomOneDark;
			}

			var variant = Application.Current?.ActualThemeVariant;
			return variant == ThemeVariant.Dark ? ThemeName.AtomOneDark : ThemeName.AtomOneLight;
		}

		static void ApplyTextMateTheme(TextMate.Installation installation, RegistryOptions registryOptions, ThemeName themeName, TextEditor textEditor)
		{
			try
			{
				installation.SetTheme(registryOptions.LoadTheme(themeName));
				// after applying the theme to the transformer/model, also apply GUI colors to the editor
				try
				{
					ApplyThemeColorsToEditor(installation, textEditor);
				}
				catch (Exception ex)
				{
								  log.Error(ex, "ApplyThemeColorsToEditor failed");
				}
			}
			catch (Exception ex)
			{
						  log.Error(ex, "Failed to apply TextMate theme");
			}
		}

		static void ApplyThemeColorsToEditor(TextMate.Installation e, TextEditor editor)
		{
			if (editor == null || e == null)
				return;

			bool TryApplyBrush(string key, Action<IBrush> apply)
			{
				if (!e.TryGetThemeColor(key, out var colorString))
					return false;
				if (!Color.TryParse(colorString, out var color))
					return false;
				apply(new SolidColorBrush(color));
				return true;
			}

			TryApplyBrush("editor.background", brush => editor.Background = brush);
			TryApplyBrush("editor.foreground", brush => editor.Foreground = brush);

			if (!TryApplyBrush("editor.selectionBackground", brush => editor.TextArea.SelectionBrush = brush))
			{
				if (Application.Current!.TryGetResource("TextAreaSelectionBrush", out var resourceObject))
				{
					if (resourceObject is IBrush brush)
						editor.TextArea.SelectionBrush = brush;
				}
			}

			if (!TryApplyBrush("editor.lineHighlightBackground", brush => {
				editor.TextArea.TextView.CurrentLineBackground = brush;
				editor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
			}))
			{
				editor.TextArea.TextView.SetDefaultHighlightLineColors();
			}

			if (!TryApplyBrush("editorLineNumber.foreground", brush => editor.LineNumbersForeground = brush))
			{
				editor.LineNumbersForeground = editor.Foreground;
			}
		}

		#region Constructor
		public DecompilerTextView() : this(ProjectRover.App.ExportProvider!)
		{
				  log.Debug("DecompilerTextView parameterless ctor called. App.ExportProvider is {State}", ProjectRover.App.ExportProvider == null ? "null" : "set");
		}

		public DecompilerTextView(IExportProvider exportProvider)
		{
				  log.Debug("DecompilerTextView ctor called with exportProvider: {State}", exportProvider == null ? "null" : "set");
			this.exportProvider = exportProvider;
			settingsService = exportProvider.GetExportedValue<SettingsService>();
			languageService = exportProvider.GetExportedValue<LanguageService>();

			RegisterHighlighting();
			// TextMate installation is optional and may be initialized elsewhere.

			InitializeComponent();

            // Ensure resources are available for BracketHighlightRenderer
            if (!this.Resources.ContainsKey("BracketHighlightBorderPen"))
            {
                this.Resources.Add("BracketHighlightBorderPen", new Avalonia.Media.Pen(new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#800000FF")), 1));
            }
            if (!this.Resources.ContainsKey("BracketHighlightBackgroundBrush"))
            {
                this.Resources.Add("BracketHighlightBackgroundBrush", new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#280000FF")));
            }

			this.referenceElementGenerator = new ReferenceElementGenerator(this.IsLink);

			// For diagnostics: subscribe to ThemeChanged messages and log them
			try
			{
				MessageBus<ThemeChangedEventArgs>.Subscribers += (s, e) =>
				{
								  log.Debug("Received ThemeChanged message: {Theme}", e.ThemeName);
				};
			}
			catch { }

			// Create a per-editor TextMate installation using compile-time RegistryOptions.
			try
			{
				var textMateTheme = ResolveTextMateTheme(null);
				var registryOptions = new RegistryOptions(textMateTheme);
				var textMateInstallation = textEditor.InstallTextMate(registryOptions);
						  log.Debug("Installed TextMate for editor.");

				ApplyTextMateTheme(textMateInstallation, registryOptions, textMateTheme, textEditor);
				// ensure GUI brushes are also applied whenever the installation signals it applied a theme
				try
				{
					textMateInstallation.AppliedTheme += (_, _) =>
					{
						try
						{
							ApplyThemeColorsToEditor(textMateInstallation, textEditor);
						}
						catch (Exception ex)
						{
									  log.Error(ex, "AppliedTheme handler failed");
						}
					};
				}
				catch { }

				MessageBus<ThemeChangedEventArgs>.Subscribers += (s, e) =>
				{
					var nextTheme = ResolveTextMateTheme(e.ThemeName);
					ApplyTextMateTheme(textMateInstallation, registryOptions, nextTheme, textEditor);
						  log.Debug("Applied TextMate theme on ThemeChanged: {Theme}", e.ThemeName);
				};
			}
			catch (Exception ex)
			{
						  log.Error(ex, "TextMate wiring skipped");
			}
			textEditor.TextArea.TextView.ElementGenerators.Add(referenceElementGenerator);
			this.uiElementGenerator = new UIElementGenerator();
			this.bracketHighlightRenderer = new BracketHighlightRenderer(textEditor.TextArea.TextView);
			textEditor.TextArea.TextView.ElementGenerators.Add(uiElementGenerator);
			textEditor.Options.RequireControlModifierForHyperlinkClick = false;
			textEditor.TextArea.TextView.PointerHover += TextViewMouseHover;
			textEditor.TextArea.TextView.PointerHoverStopped += TextViewMouseHoverStopped;
			textEditor.TextArea.AddHandler(InputElement.PointerPressedEvent, TextAreaMouseDown, RoutingStrategies.Tunnel, true);
			textEditor.TextArea.AddHandler(InputElement.PointerReleasedEvent, TextAreaMouseUp, RoutingStrategies.Tunnel, true);
			textEditor.TextArea.Caret.PositionChanged += HighlightBrackets;
			textEditor.PointerMoved += TextEditorMouseMove;
			textEditor.PointerExited += TextEditorMouseLeave;
			textEditor.Bind(TextEditor.FontFamilyProperty, new Binding { Source = settingsService.DisplaySettings, Path = "SelectedFont" });
			textEditor.Bind(TextEditor.FontSizeProperty, new Binding { Source = settingsService.DisplaySettings, Path = "SelectedFontSize" });
			textEditor.Bind(TextEditor.WordWrapProperty, new Binding { Source = settingsService.DisplaySettings, Path = "EnableWordWrap" });

			// disable Tab editing command (useless for read-only editor); allow using tab for focus navigation instead
			RemoveEditCommand(EditingCommands.TabForward);
			RemoveEditCommand(EditingCommands.TabBackward);

			textMarkerService = new TextMarkerService(textEditor.TextArea.TextView);
			textEditor.TextArea.TextView.BackgroundRenderers.Add(textMarkerService);
			textEditor.TextArea.TextView.LineTransformers.Add(textMarkerService);
			// Force show line numbers for easier diagnostics during hover debugging and log margin info
			textEditor.ShowLineNumbers = true;
			log.Debug("DisplaySettings.ShowLineNumbers = {ShowLineNumbers}", settingsService.DisplaySettings.ShowLineNumbers);
			int mi = 0;
			foreach (var margin in this.textEditor.TextArea.LeftMargins)
			{
				mi++;
				log.Debug("LeftMargin[{Index}]: Type={Type} IsVisible={IsVisible}", mi, margin.GetType().Name, margin.IsVisible);
				if (margin is LineNumberMargin || margin is Avalonia.Controls.Shapes.Line)
				{
					margin.IsVisible = true;
					log.Debug("LeftMargin[{Index}] forced visible", mi);
				}
			}

			MessageBus<SettingsChangedEventArgs>.Subscribers += Settings_Changed;

			// SearchPanel
			// SearchPanel searchPanel = SearchPanel.Install(textEditor.TextArea);
			// searchPanel.RegisterCommands(mainWindow.CommandBindings);
			// searchPanel.SetResourceReference(SearchPanel.MarkerBrushProperty, ResourceKeys.SearchResultBackgroundBrush);
			// searchPanel.Loaded += (_, _) => {
			// 	// HACK: fix search text box
			// 	var textBox = searchPanel.Template.FindName("PART_searchTextBox", searchPanel) as TextBox;
			// 	if (textBox != null)
			// 	{
			// 		// the hardcoded but misaligned margin
			// 		textBox.Margin = new Thickness(3);
			// 		// the hardcoded height
			// 		textBox.Height = double.NaN;
			// 	}
			// };

			ShowLineMargin();
			SetHighlightCurrentLine();

			ContextMenuProvider.Add(this);

			textEditor.TextArea.TextView.Bind(AvaloniaEdit.Rendering.TextView.LinkTextForegroundBrushProperty, this.GetResourceObservable(ResourceKeys.LinkTextForegroundBrush));
			textEditor.TextArea.TextView.Bind(AvaloniaEdit.Rendering.TextView.CurrentLineBackgroundProperty, this.GetResourceObservable(ResourceKeys.CurrentLineBackgroundBrush));
			textEditor.TextArea.TextView.Bind(AvaloniaEdit.Rendering.TextView.CurrentLineBorderProperty, this.GetResourceObservable(ResourceKeys.CurrentLineBorderPen));

			//DataObject.AddSettingDataHandler(textEditor.TextArea, OnSettingData);

			this.DataContextChanged += DecompilerTextView_DataContextChanged;
		}

		private void DecompilerTextView_DataContextChanged(object sender, EventArgs e)
		{
			if (this.DataContext is PaneModel model)
			{
				model.Title = currentTitle ?? Properties.Resources.Decompiling;
			}
		}

		void RemoveEditCommand(AvaloniaEdit.RoutedCommand command)
		{
			var handler = textEditor.TextArea.DefaultInputHandler.Editing;
			// TODO: no input bindings? var inputBinding = handler.InputBindings.FirstOrDefault(b => b.Command == command);
			// if (inputBinding != null)
			// 	handler.InputBindings.Remove(inputBinding);
			var commandBinding = handler.CommandBindings.FirstOrDefault(b => b.Command == command);
			if (commandBinding != null)
				handler.CommandBindings.Remove(commandBinding);
		}
		#endregion

		#region Line margin

		private void Settings_Changed(object? sender, SettingsChangedEventArgs e)
		{
			Settings_PropertyChanged(sender, e);
		}

		private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not DisplaySettings)
				return;

			switch (e.PropertyName)
			{
				case nameof(DisplaySettings.ShowLineNumbers):
					ShowLineMargin();
					break;
				case nameof(DisplaySettings.HighlightCurrentLine):
					SetHighlightCurrentLine();
					break;
			}
		}

		void ShowLineMargin()
		{
			foreach (var margin in this.textEditor.TextArea.LeftMargins)
			{
				// For diagnostics we force visibility and set a contrasting foreground
				try
				{
					if (margin is LineNumberMargin || margin is Avalonia.Controls.Shapes.Line)
					{
						margin.IsVisible = true; // force visible regardless of settings
						// set foreground to editor foreground to ensure contrast
						try { margin.SetValue(Avalonia.Controls.TextBlock.ForegroundProperty, textEditor.Foreground); } catch { }
						log.Debug("ShowLineMargin: forced visible LeftMargin type={Type}", margin.GetType().Name);
					}
				}
				catch (Exception ex)
				{
					log.Debug(ex, "ShowLineMargin: failed to force margin visibility for {Type}", margin.GetType().Name);
				}
			}
		}



		void SetHighlightCurrentLine()
		{
			textEditor.Options.HighlightCurrentLine = settingsService.DisplaySettings.HighlightCurrentLine;
		}

		#endregion

		#region Tooltip support
			Tooltip? toolTip;
			Popup? popupToolTip;
			TooltipSegmentKey? lastTooltipSegmentKey;

			void TextViewMouseHover(object sender, Avalonia.Input.PointerEventArgs e)
			{
				// WPF tooltips live in a separate window and don't re-trigger hover logic while the pointer is over them.
				// Mirror that behavior by ignoring hover events whenever the custom popup already has pointer focus/over state.
				if (popupToolTip != null && (popupToolTip.IsPointerInside || popupToolTip.IsPointerOver || (popupToolTip.Child as Control)?.IsPointerOver == true))
				{
					log.Debug("Ignoring hover while pointer is over the popup");
					return;
				}

				var mousePos = e.GetPosition(this);
				log.Debug("TextViewMouseHover triggered at ({X}, {Y}) relative to DecompilerTextView", mousePos.X, mousePos.Y);
				log.Debug("DecompilerTextView bounds: Width={Width}, Height={Height}, IsVisible={IsVisible}", Bounds.Width, Bounds.Height, IsVisible);
				log.Debug("TextEditor: IsInitialized={IsInit}, Document={HasDoc}, TextView={HasView}", 
					textEditor != null, 
					textEditor?.Document != null, 
					textEditor?.TextArea?.TextView != null);
				
				TextViewPosition? position = GetPositionFromMousePosition(mousePos);
				if (position == null)
				{
					// This usually means the pointer is over the popup or outside the text view; WPF would ignore it.
					log.Debug("GetPositionFromMousePosition returned null for mouse at ({X}, {Y})", mousePos.X, mousePos.Y);
					return;
				}
			
			int offset = textEditor.Document.GetOffset(position.Value.Location);
			log.Debug("Mouse hover at offset {Offset}, line {Line}, column {Column}", offset, position.Value.Line, position.Value.Column);
			
			// Log the line text and a surrounding snippet so we can see what the cursor is over
			var docLine = textEditor.Document.GetLineByNumber(position.Value.Line);
			var lineText = textEditor.Document.GetText(docLine.Offset, docLine.Length);
			int indexInLine = offset - docLine.Offset;
			int snippetStart = Math.Max(0, indexInLine - 40);
			int snippetLen = Math.Min(80, Math.Max(0, Math.Min(docLine.Length - snippetStart, 80)));
			var snippet = snippetLen > 0 ? lineText.Substring(snippetStart, snippetLen) : string.Empty;
			char? charUnderCursor = (indexInLine >= 0 && indexInLine < lineText.Length) ? lineText[indexInLine] : (char?)null;
			log.Debug("Line {Line} (len={Len}) indexInLine={Index} charUnderCursor={Char} snippet=\"{Snippet}\"", position.Value.Line, docLine.Length, indexInLine, charUnderCursor?.ToString() ?? "(none)", snippet);

			// Log document contents intelligently: full text when small, otherwise a focused excerpt
			int docLength = textEditor.Document.TextLength;
			const int fullDumpLimit = 20000; // characters
			if (docLength <= fullDumpLimit)
			{
				var fullText = textEditor.Document.Text;
				log.Verbose("Document full text (len={Len}): {DocText}", docLength, fullText);
			}
			else
			{
				int startLine = Math.Max(1, position.Value.Line - 10);
				int endLine = Math.Min(textEditor.Document.LineCount, position.Value.Line + 10);
				var excerpt = new System.Text.StringBuilder();
				for (int ln = startLine; ln <= endLine; ln++)
				{
					var lobj = textEditor.Document.GetLineByNumber(ln);
					var txt = textEditor.Document.GetText(lobj.Offset, lobj.Length);
					excerpt.AppendLine($"{ln,5}: {txt}");
				}
				log.Verbose("Document excerpt lines {Start}-{End} around line {Line}:\n{Excerpt}", startLine, endLine, position.Value.Line, excerpt.ToString());
			}
			
			if (referenceElementGenerator.References == null)
			{
				log.Debug("referenceElementGenerator.References is null, aborting tooltip");
				return;
			}
			
			ReferenceSegment? seg = referenceElementGenerator.References.FindSegmentsContaining(offset).FirstOrDefault();
			if (seg == null)
			{
				log.Debug("No reference segment found at offset {Offset}", offset);
				return;
			}

			if (IsSameTooltipSegment(seg) && popupToolTip?.IsOpen == true)
			{
				var popupPosition = GetPopupPosition(e, position.Value);
				popupToolTip.HorizontalOffset = popupPosition.X;
				popupToolTip.VerticalOffset = popupPosition.Y;
				log.Debug("Reusing existing popup for segment {Start}-{End}", seg.StartOffset, seg.EndOffset);
				return;
			}

			var refText = textEditor.Document.GetText(seg.StartOffset, seg.Length);
			log.Debug("Found reference segment: {SegmentType} at {Start}-{End} (len={Len}) text=\"{RefText}\"", seg.Reference?.GetType().Name ?? "null", seg.StartOffset, seg.EndOffset, seg.Length, refText);
			object? content = GenerateTooltip(seg);
			
			// Also log the identifier/token under or near the cursor by scanning word characters
			string lineFullText = lineText;
			int start = indexInLine;
			int end = indexInLine;
			// if cursor is at end of line, move back one char for token detection
			if (start >= lineFullText.Length && lineFullText.Length > 0)
				start = end = lineFullText.Length - 1;
			// expand start left while word chars
			while (start > 0 && IsIdentifierChar(lineFullText[start - 1])) start--;
			// expand end right while word chars
			while (end < lineFullText.Length && IsIdentifierChar(lineFullText[end])) end++;
			string token = (start < end && start >= 0 && end <= lineFullText.Length) ? lineFullText.Substring(start, end - start) : string.Empty;
			log.Debug("Token under/near cursor: \"{Token}\" (startInLine={Start}, endInLine={End})", token, start, end);
			
			// Log up to 3 nearby reference segments overlapping a small neighborhood (±10 chars)
			int neighborhoodStart = Math.Max(docLine.Offset, offset - 10);
			int neighborhoodEnd = Math.Min(docLine.EndOffset, offset + 10);
			var nearby = referenceElementGenerator.References
				.Where(s => s.StartOffset < neighborhoodEnd && s.EndOffset > neighborhoodStart)
				.Take(3)
				.ToArray();
			for (int i = 0; i < nearby.Length; i++)
			{
				var n = nearby[i];
				var nText = textEditor.Document.GetText(n.StartOffset, n.Length);
				log.Verbose("Nearby ref[{Index}]: {Type} {Start}-{End} len={Len} text=\"{Text}\"", i, n.Reference?.GetType().Name ?? "null", n.StartOffset, n.EndOffset, n.Length, nText);
			}

			if (content == null)
			{
				log.Debug("GenerateTooltip returned null");
				return;
			}

			if (!TryCloseExistingPopup(false))
			{
				return;
			}

			log.Information("Generated tooltip content: {ContentType}", content.GetType().FullName);
			popupToolTip = content as Popup;

			if (popupToolTip != null)
			{
				log.Information("Showing Popup tooltip");
				var popupPosition = GetPopupPosition(e, position.Value);
				var cursorInView = e.GetPosition(this);
				var cursorInEditor = e.GetPosition(textEditor);
				var cursorInTextView = e.GetPosition(textEditor.TextArea.TextView);
				var deltaX = popupPosition.X - cursorInTextView.X;
				var deltaY = popupPosition.Y - cursorInTextView.Y;
				log.Debug("Popup position: ({X}, {Y}); cursor view=({CX}, {CY}), editor=({EX}, {EY}), textView=({TX}, {TY}) delta=({DX}, {DY})",
					popupPosition.X, popupPosition.Y, cursorInView.X, cursorInView.Y, cursorInEditor.X, cursorInEditor.Y,
					cursorInTextView.X, cursorInTextView.Y, deltaX, deltaY);
				popupToolTip.Closed += ToolTipClosed;
				popupToolTip.Placement = PlacementMode.Pointer;
				popupToolTip.PlacementTarget = textEditor.TextArea.TextView;
				popupToolTip.HorizontalOffset = popupPosition.X;
				popupToolTip.VerticalOffset = popupPosition.Y;
				popupToolTip.StaysOpen = true; // We will close it ourselves

				e.Handled = true;
				popupToolTip.IsOpen = true;
				lastTooltipSegmentKey = TooltipSegmentKey.FromSegment(seg);
				log.Information("Popup IsOpen set to true");
				log.Debug("Popup child present: {HasChild}, childRoot={ChildRoot}", popupToolTip.Child != null, popupToolTip.Child?.GetVisualRoot() != null);
				distanceToPopupLimit = double.PositiveInfinity; // reset limit; we'll re-calculate it on the next mouse movement
			}
			else
			{
				if (toolTip == null)
				{
					toolTip = new Tooltip();
					toolTip.Closed += ToolTipClosed;
				}
				var popupPosition = GetPopupPosition(e, position.Value);
				toolTip.Placement = PlacementMode.Pointer;
				toolTip.PlacementTarget = textEditor.TextArea.TextView; // required for property inheritance
				toolTip.HorizontalOffset = popupPosition.X;
				toolTip.VerticalOffset = popupPosition.Y;
				toolTip.SetContent(content, settingsService.DisplaySettings.SelectedFontSize, MainWindowInstance.Width);

				e.Handled = true;
				toolTip.IsOpen = true;
			}
		}

		bool TryCloseExistingPopup(bool mouseClick)
		{
			if (popupToolTip != null)
			{
				if (popupToolTip.IsOpen && !mouseClick && popupToolTip is FlowDocumentTooltip t && !t.CloseWhenMouseMovesAway)
				{
					return false; // Popup does not want to be closed yet
				}
				popupToolTip.IsOpen = false;
				popupToolTip = null;
				lastTooltipSegmentKey = null;
			}
			return true;
		}

		bool IsSameTooltipSegment(ReferenceSegment segment)
		{
			return lastTooltipSegmentKey.HasValue && lastTooltipSegmentKey.Value.Equals(TooltipSegmentKey.FromSegment(segment));
		}

		/// <summary> Returns Popup position based on mouse position, in device independent units </summary>
		Point GetPopupPosition(Avalonia.Input.PointerEventArgs mouseArgs, TextViewPosition position)
		{
			var textView = textEditor.TextArea.TextView;
			var mouseInTextView = mouseArgs.GetPosition(textView);
			log.Debug("GetPopupPosition: mouse textView=({X}, {Y}), scroll=({SX}, {SY})",
				mouseInTextView.X, mouseInTextView.Y, textView.ScrollOffset.X, textView.ScrollOffset.Y);
			// align Popup with line bottom for the resolved position, but keep X near the cursor
			var visualPos = textView.GetVisualPosition(position, VisualYPosition.LineBottom);
			var desired = new Point(
				mouseInTextView.X - 4,
				visualPos.Y - textView.ScrollOffset.Y);
			var offset = new Point(desired.X - mouseInTextView.X, desired.Y - mouseInTextView.Y);
			log.Debug("GetPopupPosition: logical={Line},{Col} visual=({VX}, {VY}) desired=({DX}, {DY}) offset=({OX}, {OY})",
				position.Line, position.Column, visualPos.X, visualPos.Y, desired.X, desired.Y, offset.X, offset.Y);
			return offset;
		}

		void TextViewMouseHoverStopped(object sender, Avalonia.Input.PointerEventArgs e)
		{
			// Non-popup tooltips get closed as soon as the mouse starts moving again
			if (toolTip != null)
			{
				toolTip.IsOpen = false;
				e.Handled = true;
			}
		}

		double distanceToPopupLimit;
		const double MaxMovementAwayFromPopup = 5;

		void TextEditorMouseMove(object sender, Avalonia.Input.PointerEventArgs e)
		{
			if (popupToolTip != null && popupToolTip.Child?.GetVisualRoot() != null)
			{
				double distanceToPopup = GetDistanceToPopup(e);
				if (distanceToPopup > distanceToPopupLimit)
				{
					// Close popup if mouse moved away, exceeding the limit
					TryCloseExistingPopup(false);
				}
				else
				{
					// reduce distanceToPopupLimit
					distanceToPopupLimit = Math.Min(distanceToPopupLimit, distanceToPopup + MaxMovementAwayFromPopup);
				}
			}
		}

		double GetDistanceToPopup(Avalonia.Input.PointerEventArgs e)
		{
			if (popupToolTip?.Child is not Control child)
				return double.PositiveInfinity;

			var mouseInSelf = e.GetPosition(this);
			var mouseInScreen = this.PointToScreen(mouseInSelf);
			var popupOriginInScreen = child.PointToScreen(new Point(0, 0));
			var p = new Point(mouseInScreen.X - popupOriginInScreen.X, mouseInScreen.Y - popupOriginInScreen.Y);
			var size = child.Bounds.Size;
			double x = 0;
			if (p.X < 0)
				x = -p.X;
			else if (p.X > size.Width)
				x = p.X - size.Width;
			double y = 0;
			if (p.Y < 0)
				y = -p.Y;
			else if (p.Y > size.Height)
				y = p.Y - size.Height;
			return Math.Sqrt(x * x + y * y);
		}

		void TextEditorMouseLeave(object sender, Avalonia.Input.PointerEventArgs e)
		{
			if (popupToolTip == null)
				return;

			// Mirror WPF: keep the popup open while the pointer is moving into it.
			// Avalonia raises PointerExited on the editor before PointerEntered on the popup,
			// so rely on current pointer-over state or proximity instead of closing immediately.
			if (popupToolTip.IsPointerInside || popupToolTip.IsPointerOver || (popupToolTip.Child as Control)?.IsPointerOver == true)
				return;

			// If the pointer is still close to the popup, allow the distance-based logic in MouseMove to manage closure.
			var distanceToPopup = GetDistanceToPopup(e);
			if (distanceToPopup <= MaxMovementAwayFromPopup)
			{
				distanceToPopupLimit = Math.Min(distanceToPopupLimit, distanceToPopup + MaxMovementAwayFromPopup);
				return;
			}

			TryCloseExistingPopup(false);
		}

		void OnUnloaded(object sender, EventArgs e)
		{
			// Close popup when another document gets selected
			// TextEditorMouseLeave is not sufficient for this because the mouse might be over the popup when the document switch happens (e.g. Ctrl+Tab)
			TryCloseExistingPopup(true);
		}

		void ToolTipClosed(object? sender, EventArgs e)
		{
			if (toolTip == sender)
				toolTip = null;
			if (popupToolTip == sender)
			{
				popupToolTip.Closed -= ToolTipClosed;
				popupToolTip = null;
				lastTooltipSegmentKey = null;
			}
		}

		object? GenerateTooltip(ReferenceSegment segment)
		{
			log.Debug("GenerateTooltip called for segment reference type: {RefType}", segment.Reference?.GetType().Name ?? "null");
			var fontSize = settingsService.DisplaySettings.SelectedFontSize;

			if (segment.Reference is ICSharpCode.Decompiler.Disassembler.OpCodeInfo code)
			{
				log.Debug("Generating OpCode tooltip for: {OpCodeName}", code.Name);
				XmlDocumentationProvider docProvider = XmlDocLoader.MscorlibDocumentation;
				DocumentationUIBuilder renderer = new DocumentationUIBuilder(new CSharpAmbience(), languageService.Language.SyntaxHighlighting, settingsService.DisplaySettings, MainWindowInstance);
				renderer.AddSignatureBlock($"{code.Name} (0x{code.Code:x})");
				if (docProvider != null)
				{
					string documentation = docProvider.GetDocumentation("F:System.Reflection.Emit.OpCodes." + code.EncodedName);
					if (documentation != null)
					{
						renderer.AddXmlDocumentation(documentation, null, null);
					}
				}
				var document = renderer.CreateDocument();
				log.Debug("Created FlowDocumentTooltip for OpCode");
				return new FlowDocumentTooltip(document, fontSize, MainWindowInstance.Width);
			}
			else if (segment.Reference is IEntity entity)
			{
				log.Debug("Generating entity tooltip for: {EntityName}", entity.Name);
				try
				{
					var documentControl = CreateTooltipForEntity(entity);
					if (documentControl == null)
					{
						log.Debug("CreateTooltipForEntity returned null");
						return null;
					}
					log.Debug("Created FlowDocumentTooltip for entity");
					return new FlowDocumentTooltip(documentControl, fontSize, MainWindowInstance.Width);
				}
				catch (Exception ex)
				{
					log.Debug(ex, "CreateTooltipForEntity failed; falling back to plain text tooltip");
					var fallback = new TextBlock { Text = entity.Name, TextWrapping = TextWrapping.Wrap, FontSize = fontSize };
					return new FlowDocumentTooltip(fallback, fontSize, MainWindowInstance.Width);
				}
			}
			else if (segment.Reference is EntityReference unresolvedEntity)
			{
				var assemblyList = exportProvider.GetExportedValue<AssemblyList>();
				var module = unresolvedEntity.ResolveAssembly(assemblyList);
				if (module == null)
					return null;
				var typeSystem = new DecompilerTypeSystem(module,
					module.GetAssemblyResolver(),
					TypeSystemOptions.Default | TypeSystemOptions.Uncached);
				try
				{
					Handle handle = unresolvedEntity.Handle;
					if (!handle.IsEntityHandle())
						return null;
					IEntity resolved = typeSystem.MainModule.ResolveEntity((EntityHandle)handle);
					if (resolved == null)
						return null;
					var documentControl = CreateTooltipForEntity(resolved);
						if (documentControl == null)
							return null;
						return new FlowDocumentTooltip(documentControl, fontSize, MainWindowInstance.Width);
				}
				catch (BadImageFormatException)
				{
					return null;
				}
			}
			return null;
		}

		Control? CreateTooltipForEntity(IEntity resolved)
		{
			Language currentLanguage = languageService.Language;
			DocumentationUIBuilder renderer = new DocumentationUIBuilder(new CSharpAmbience(), currentLanguage.SyntaxHighlighting, settingsService.DisplaySettings, MainWindowInstance);
			RichText richText = currentLanguage.GetRichTextTooltip(resolved);
			if (richText == null)
			{
				return null;
			}

			renderer.AddSignatureBlock(richText.Text, richText.ToRichTextModel());
			try
			{
				var metadataFile = resolved.ParentModule?.MetadataFile;
				if (metadataFile != null)
				{
					var docProvider = XmlDocLoader.LoadDocumentation(metadataFile);
					if (docProvider != null)
					{
						string documentation = docProvider.GetDocumentation(resolved.GetIdString());
						if (documentation != null)
						{
							renderer.AddXmlDocumentation(documentation, resolved, ResolveReference);
						}
					}
				}
			}
			catch (XmlException)
			{
				// ignore
			}
			return renderer.CreateDocument();

			IEntity? ResolveReference(string idString)
			{
				var assemblyList = exportProvider.GetExportedValue<AssemblyList>();
				return AssemblyTreeModel.FindEntityInRelevantAssemblies(idString, assemblyList.GetAssemblies());
			}
		}

		// Tooltip and Popup moved to TextView/Controls/RoverTooltipPopup.cs (namespace ICSharpCode.ILSpy.TextView.Controls)

		sealed class FlowDocumentTooltip : Popup
		{
			public FlowDocumentTooltip(Control documentControl, double fontSize, double maxWith)
			{
				if (documentControl is TextBlock textBlock && fontSize > 0 && double.IsFinite(fontSize))
					textBlock.FontSize = fontSize;
				SetContent(documentControl, maxWith);
				StaysOpen = true;
			}

			protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e)
			{
				base.OnLostFocus(e);
				IsOpen = false;
			}

			protected override void OnPointerInsideChanged(bool isInside)
			{
				if (!isInside && CloseWhenMouseMovesAway)
					IsOpen = false;
			}
		}


		readonly struct TooltipSegmentKey : IEquatable<TooltipSegmentKey>
		{
			public TooltipSegmentKey(int startOffset, int endOffset, Type? referenceType)
			{
				StartOffset = startOffset;
				EndOffset = endOffset;
				ReferenceType = referenceType;
			}

			public int StartOffset { get; }
			public int EndOffset { get; }
			public Type? ReferenceType { get; }

			public static TooltipSegmentKey FromSegment(ReferenceSegment segment)
			{
				return new TooltipSegmentKey(segment.StartOffset, segment.EndOffset, segment.Reference?.GetType());
			}

			public bool Equals(TooltipSegmentKey other)
			{
				return StartOffset == other.StartOffset
					&& EndOffset == other.EndOffset
					&& ReferenceType == other.ReferenceType;
			}

			public override bool Equals(object? obj)
			{
				return obj is TooltipSegmentKey other && Equals(other);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(StartOffset, EndOffset, ReferenceType);
			}
		}

		#endregion

		#region Highlight brackets
		void HighlightBrackets(object? sender, EventArgs e)
		{
			if (settingsService.DisplaySettings.HighlightMatchingBraces)
			{
				var result = languageService.Language.BracketSearcher.SearchBracket(textEditor.Document, textEditor.CaretOffset);
				bracketHighlightRenderer.SetHighlight(result);
			}
			else
			{
				bracketHighlightRenderer.SetHighlight(null);
			}
		}
		#endregion

		#region RunWithCancellation
		public void Report(DecompilationProgress value)
		{
			double v = (double)value.UnitsCompleted / value.TotalUnits;
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, delegate {
				progressBar.IsIndeterminate = !double.IsFinite(v);
				progressBar.Value = v * 100.0;
				progressTitle.Text = !string.IsNullOrWhiteSpace(value.Title) ? value.Title : Properties.Resources.Decompiling;
				progressText.Text = value.Status;
				progressText.IsVisible = !string.IsNullOrWhiteSpace(progressText.Text);
				// TODO: Windows only. var taskBar = mainWindow.TaskbarItemInfo;
				// if (taskBar != null)
				// {
				// 	taskBar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
				// 	taskBar.ProgressValue = v;
				// }
				if (this.DataContext is TabPageModel model)
				{
					model.Title = progressTitle.Text;
				}
			});
		}

		/// <summary>
		/// Switches the GUI into "waiting" mode, then calls <paramref name="taskCreation"/> to create
		/// the task.
		/// If another task is started before the previous task finishes running, the previous task is cancelled.
		/// </summary>
		public Task<T> RunWithCancellation<T>(Func<CancellationToken, Task<T>> taskCreation, string? progressTitle = null)
		{
			if (!waitAdorner.IsVisible)
			{
				waitAdorner.IsVisible = true;
				// Work around a WPF bug by setting IsIndeterminate only while the progress bar is visible.
				// https://github.com/icsharpcode/ILSpy/issues/593
				this.progressTitle.Text = progressTitle == null ? Properties.Resources.Decompiling : progressTitle;
				progressBar.IsIndeterminate = true;
				progressText.Text = null;
				progressText.IsVisible = false;
				// waitAdorner.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)), FillBehavior.Stop));
				// var taskBar = mainWindow.TaskbarItemInfo;
				// if (taskBar != null)
				// {
				// 	taskBar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
				// }
			}
			CancellationTokenSource? previousCancellationTokenSource = currentCancellationTokenSource;
			var myCancellationTokenSource = new CancellationTokenSource();
			currentCancellationTokenSource = myCancellationTokenSource;
			// cancel the previous only after current was set to the new one (avoid that the old one still finishes successfully)
			if (previousCancellationTokenSource != null)
			{
				previousCancellationTokenSource.Cancel();
			}

			var tcs = new TaskCompletionSource<T>();
			Task<T> task;
			try
			{
				task = taskCreation(myCancellationTokenSource.Token);
			}
			catch (OperationCanceledException)
			{
				task = TaskHelper.FromCancellation<T>();
			}
			catch (Exception ex)
			{
				task = TaskHelper.FromException<T>(ex);
			}
			Action continuation = delegate {
				try
				{
					if (currentCancellationTokenSource == myCancellationTokenSource)
					{
						currentCancellationTokenSource = null;
						waitAdorner.IsVisible = false;
						progressBar.IsIndeterminate = false;
						progressText.Text = null;
						progressText.IsVisible = false;
						// TODO: windows only var taskBar = mainWindow.TaskbarItemInfo;
						//if (taskBar != null)
						//{
							//taskBar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
						//}
						if (task.IsCanceled)
						{
							AvalonEditTextOutput output = new AvalonEditTextOutput();
							output.WriteLine("The operation was canceled.");
							ShowOutput(output);
						}
						tcs.SetFromTask(task);
					}
					else
					{
						tcs.SetCanceled();
					}
				}
				finally
				{
					myCancellationTokenSource.Dispose();
				}
			};
			task.ContinueWith(delegate { Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, continuation); });
			return tcs.Task;
		}

		void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (currentCancellationTokenSource != null)
			{
				currentCancellationTokenSource.Cancel();
				// Don't set to null: the task still needs to produce output and hide the wait adorner
			}
		}
		#endregion

		#region ShowOutput
		public void ShowText(AvalonEditTextOutput textOutput)
		{
			ShowNodes(textOutput, null);
		}

		public void ShowNode(AvalonEditTextOutput textOutput, ILSpyTreeNode node, IHighlightingDefinition? highlighting = null)
		{
			ShowNodes(textOutput, new[] { node }, highlighting);
		}

		/// <summary>
		/// Shows the given output in the text view.
		/// Cancels any currently running decompilation tasks.
		/// </summary>
		public void ShowNodes(AvalonEditTextOutput textOutput, ILSpyTreeNode[]? nodes, IHighlightingDefinition? highlighting = null)
		{
			// Cancel the decompilation task:
			if (currentCancellationTokenSource != null)
			{
				currentCancellationTokenSource.Cancel();
				currentCancellationTokenSource = null; // prevent canceled task from producing output
			}
			if (this.nextDecompilationRun != null)
			{
				// remove scheduled decompilation run
				this.nextDecompilationRun.TaskCompletionSource.TrySetCanceled();
				this.nextDecompilationRun = null;
			}
			if (nodes != null && (string.IsNullOrEmpty(textOutput.Title)
				|| textOutput.Title == Properties.Resources.NewTab))
			{
				textOutput.Title = string.Join(", ", nodes.Select(n => n.Text));
			}

			decompiledNodes = nodes;
			ShowOutput(textOutput, highlighting);
		}

		/// <summary>
		/// Shows the given output in the text view.
		/// </summary>
		void ShowOutput(AvalonEditTextOutput textOutput, IHighlightingDefinition? highlighting = null, DecompilerTextViewState? state = null)
		{
			Debug.WriteLine("Showing {0} characters of output", textOutput.TextLength);
			Stopwatch w = Stopwatch.StartNew();

			ClearLocalReferenceMarks();
			textEditor.ScrollToHome();
			if (foldingManager != null)
			{
				FoldingManager.Uninstall(foldingManager);
				foldingManager = null;
			}
			textEditor.Document = null; // clear old document while we're changing the highlighting
			uiElementGenerator.UIElements = textOutput.UIElements;
			referenceElementGenerator.References = textOutput.References;
			references = textOutput.References;
			definitionLookup = textOutput.DefinitionLookup;
			textEditor.SyntaxHighlighting = highlighting;
			textEditor.Options.EnableEmailHyperlinks = textOutput.EnableHyperlinks;
			textEditor.Options.EnableHyperlinks = textOutput.EnableHyperlinks;
			activeRichTextModel = null;
			if (activeRichTextColorizer != null)
				textEditor.TextArea.TextView.LineTransformers.Remove(activeRichTextColorizer);
			if (textOutput.HighlightingModel != null)
			{
				activeRichTextModel = textOutput.HighlightingModel;
				activeRichTextColorizer = new RichTextColorizer(textOutput.HighlightingModel);
				textEditor.TextArea.TextView.LineTransformers.Insert(highlighting == null ? 0 : 1, activeRichTextColorizer);
			}

			// Change the set of active element generators:
			foreach (var elementGenerator in activeCustomElementGenerators)
			{
				textEditor.TextArea.TextView.ElementGenerators.Remove(elementGenerator);
			}
			activeCustomElementGenerators.Clear();

			foreach (var elementGenerator in textOutput.elementGenerators)
			{
				textEditor.TextArea.TextView.ElementGenerators.Add(elementGenerator);
				activeCustomElementGenerators.Add(elementGenerator);
			}

			Debug.WriteLine("  Set-up: {0}", w.Elapsed);
			w.Restart();
			textEditor.Document = textOutput.GetDocument();
			Debug.WriteLine("  Assigning document: {0}", w.Elapsed);
			w.Restart();
			if (textOutput.Foldings.Count > 0)
			{
				if (state != null)
				{
					state.RestoreFoldings(textOutput.Foldings, settingsService.DisplaySettings.ExpandMemberDefinitions);
					textEditor.ScrollToVerticalOffset(state.VerticalOffset);
					textEditor.ScrollToHorizontalOffset(state.HorizontalOffset);
				}
				foldingManager = FoldingManager.Install(textEditor.TextArea);
				foldingManager.UpdateFoldings(textOutput.Foldings.OrderBy(f => f.StartOffset), -1);
				Debug.WriteLine("  Updating folding: {0}", w.Elapsed);
				w.Restart();
			}
			else if (highlighting?.Name == "XML")
			{
				foldingManager = FoldingManager.Install(textEditor.TextArea);
				var foldingStrategy = new XmlFoldingStrategy();
				foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
				Debug.WriteLine("  Updating folding: {0}", w.Elapsed);
				w.Restart();
			}

			if (this.DataContext is PaneModel model)
			{
				model.Title = textOutput.Title;
			}
			currentAddress = textOutput.Address;
			currentTitle = textOutput.Title;
			expandMemberDefinitions = settingsService.DisplaySettings.ExpandMemberDefinitions;
			SetLocalReferenceMarks(textOutput.InitialHighlightReference);
		}
		#endregion

		#region Decompile (for display)
		// more than 5M characters is too slow to output (when user browses treeview)
		public const int DefaultOutputLengthLimit = 5000000;

		// more than 75M characters can get us into trouble with memory usage
		public const int ExtendedOutputLengthLimit = 75000000;

		DecompilationContext? nextDecompilationRun;

		[Obsolete("Use DecompileAsync() instead")]
		public void Decompile(ILSpy.Language language, IEnumerable<ILSpyTreeNode> treeNodes, DecompilationOptions options)
		{
			DecompileAsync(language, treeNodes, null, options).HandleExceptions();
		}

		/// <summary>
		/// Starts the decompilation of the given nodes.
		/// The result is displayed in the text view.
		/// If any errors occur, the error message is displayed in the text view, and the task returned by this method completes successfully.
		/// If the operation is cancelled (by starting another decompilation action); the returned task is marked as cancelled.
		/// </summary>
		public Task DecompileAsync(ILSpy.Language language, IEnumerable<ILSpyTreeNode> treeNodes, object? source, DecompilationOptions options)
		{
			// Some actions like loading an assembly list cause several selection changes in the tree view,
			// and each of those will start a decompilation action.

			bool isDecompilationScheduled = this.nextDecompilationRun != null;
			if (this.nextDecompilationRun != null)
				this.nextDecompilationRun.TaskCompletionSource.TrySetCanceled();
			this.nextDecompilationRun = new DecompilationContext(language, treeNodes.ToArray(), options, source);
			var task = this.nextDecompilationRun.TaskCompletionSource.Task;
			if (!isDecompilationScheduled)
			{
				Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(
					delegate {
						var context = this.nextDecompilationRun;
						this.nextDecompilationRun = null;
						if (context != null)
							DoDecompile(context, DefaultOutputLengthLimit)
								.ContinueWith(t => context.TaskCompletionSource.SetFromTask(t)).HandleExceptions();
					}
				));
			}
			return task;
		}

		sealed class DecompilationContext
		{
			public readonly ILSpy.Language Language;
			public readonly ILSpyTreeNode[] TreeNodes;
			public readonly DecompilationOptions Options;
			public readonly TaskCompletionSource<object?> TaskCompletionSource = new TaskCompletionSource<object?>();
			public readonly object? Source;

			public DecompilationContext(ILSpy.Language language, ILSpyTreeNode[] treeNodes, DecompilationOptions options, object? source = null)
			{
				this.Language = language;
				this.TreeNodes = treeNodes;
				this.Options = options;
				this.Source = source;
			}
		}

		Task DoDecompile(DecompilationContext context, int outputLengthLimit)
		{
			return RunWithCancellation(
				delegate (CancellationToken ct) { // creation of the background task
					context.Options.CancellationToken = ct;
					context.Options.Progress = this;
					decompiledNodes = context.TreeNodes;
					return DecompileAsync(context, outputLengthLimit);
				})
			.Then(
				delegate (AvalonEditTextOutput textOutput) { // handling the result
					ShowOutput(textOutput, context.Language.SyntaxHighlighting, context.Options.TextViewState);
				})
			.Catch<Exception>(exception => {
				textEditor.SyntaxHighlighting = null;
				Debug.WriteLine("Decompiler crashed: " + exception.ToString());
				AvalonEditTextOutput output = new AvalonEditTextOutput();
				if (exception is OutputLengthExceededException)
				{
					WriteOutputLengthExceededMessage(output, context, outputLengthLimit == DefaultOutputLengthLimit);
				}
				else
				{
					output.WriteLine(exception.ToString());
				}
				ShowOutput(output);
			});
		}

		Task<AvalonEditTextOutput> DecompileAsync(DecompilationContext context, int outputLengthLimit)
		{
			Debug.WriteLine("Start decompilation of {0} tree nodes", context.TreeNodes.Length);

			TaskCompletionSource<AvalonEditTextOutput> tcs = new TaskCompletionSource<AvalonEditTextOutput>();
			if (context.TreeNodes.Length == 0)
			{
				// If there's nothing to be decompiled, don't bother starting up a thread.
				// (Improves perf in some cases since we don't have to wait for the thread-pool to accept our task)
				tcs.SetResult(new AvalonEditTextOutput());
				return tcs.Task;
			}

			Thread thread = new Thread(new ThreadStart(
				delegate {
					try
					{
						AvalonEditTextOutput textOutput = new AvalonEditTextOutput();
						textOutput.LengthLimit = outputLengthLimit;
						textOutput.SetInitialHighlight(context.Source);
						DecompileNodes(context, textOutput);
						textOutput.PrepareDocument();
						tcs.SetResult(textOutput);
					}
					catch (OperationCanceledException)
					{
						tcs.SetCanceled();
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				}));
			thread.Start();
			return tcs.Task;
		}

		void DecompileNodes(DecompilationContext context, ITextOutput textOutput)
		{
			var nodes = context.TreeNodes;
			if (textOutput is ISmartTextOutput smartTextOutput)
			{
				smartTextOutput.Title = string.Join(", ", nodes.Select(n => n.Text));
			}
			for (int i = 0; i < nodes.Length; i++)
			{
				if (i > 0)
					textOutput.WriteLine();

				context.Options.CancellationToken.ThrowIfCancellationRequested();
				nodes[i].Decompile(context.Language, textOutput, context.Options);
			}
		}
		#endregion

		#region WriteOutputLengthExceededMessage
		/// <summary>
		/// Creates a message that the decompiler output was too long.
		/// The message contains buttons that allow re-trying (with larger limit) or saving to a file.
		/// </summary>
		void WriteOutputLengthExceededMessage(ISmartTextOutput output, DecompilationContext context, bool wasNormalLimit)
		{
			if (wasNormalLimit)
			{
				output.WriteLine("You have selected too much code for it to be displayed automatically.");
			}
			else
			{
				output.WriteLine("You have selected too much code; it cannot be displayed here.");
			}
			output.WriteLine();
			if (wasNormalLimit)
			{
				output.AddButton(
					Images.ViewCode, Properties.Resources.DisplayCode,
					delegate {
						DoDecompile(context, ExtendedOutputLengthLimit).HandleExceptions();
					});
				output.WriteLine();
			}

			output.AddButton(
				Images.Save, Properties.Resources.SaveCode,
				delegate {
					SaveToDisk(context.Language, context.TreeNodes, context.Options);
				});
			output.WriteLine();
		}
		#endregion

		#region JumpToReference
		/// <summary>
		/// Jumps to the definition referred to by the <see cref="ReferenceSegment"/>.
		/// </summary>
		internal void JumpToReference(ReferenceSegment referenceSegment, bool openInNewTab)
		{
			object reference = referenceSegment.Reference;
			if (referenceSegment.IsLocal)
			{
				ClearLocalReferenceMarks();
				SetLocalReferenceMarks(reference);
				return;
			}
			if (definitionLookup != null)
			{
				int pos = definitionLookup.GetDefinitionPosition(reference);
				if (pos >= 0)
				{
					textEditor.TextArea.Focus();
					textEditor.Select(pos, 0);
					textEditor.ScrollTo(textEditor.TextArea.Caret.Line, textEditor.TextArea.Caret.Column);
					// TODO: Dispatcher.Invoke(DispatcherPriority.Background, new Action(
					// 	delegate {
					// 		CaretHighlightAdorner.DisplayCaretHighlightAnimation(textEditor.TextArea);
					// 	}));
					return;
				}
			}
			MessageBus.Send(this, new NavigateToReferenceEventArgs(reference, openInNewTab));
		}

		private void SetLocalReferenceMarks(object reference)
		{
			if (references == null || reference == null)
			{
				return;
			}
			foreach (var r in references)
			{
				if (reference.Equals(r.Reference))
				{
					var mark = textMarkerService.Create(r.StartOffset, r.Length);
					//mark.BackgroundColor = (Color)(r.IsDefinition ? FindResource(ResourceKeys.TextMarkerDefinitionBackgroundColor) : FindResource(ResourceKeys.TextMarkerBackgroundColor));
					localReferenceMarks.Add(mark);
				}
			}
		}

		Point? mouseDownPos;

		void TextAreaMouseDown(object sender, PointerPressedEventArgs e)
		{
			mouseDownPos = e.GetPosition(this);
		}

		void TextAreaMouseUp(object sender, PointerReleasedEventArgs e)
		{
			if (mouseDownPos == null)
				return;
			Vector dragDistance = e.GetPosition(this) - mouseDownPos.Value;
			mouseDownPos = null;
			if (Math.Abs(dragDistance.X) < SystemParameters.MinimumHorizontalDragDistance
				&& Math.Abs(dragDistance.Y) < SystemParameters.MinimumVerticalDragDistance
				&& (e.InitialPressMouseButton == MouseButton.Left || e.InitialPressMouseButton == MouseButton.Middle))
			{
				// click without moving pointer
				var referenceSegment = GetReferenceSegmentAtMousePosition(e.GetPosition(this));
				if (referenceSegment == null)
				{
					ClearLocalReferenceMarks();
					return;
				}
				if (referenceSegment.IsLocal || !referenceSegment.IsDefinition)
				{
					textEditor.TextArea.ClearSelection();
					JumpToReference(referenceSegment, e.InitialPressMouseButton == MouseButton.Middle || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
					e.Handled = true;
				}
			}
		}

		void ClearLocalReferenceMarks()
		{
			foreach (var mark in localReferenceMarks)
			{
				textMarkerService.Remove(mark);
			}
			localReferenceMarks.Clear();
		}

		/// <summary>
		/// Filters all ReferenceSegments that are no real links.
		/// </summary>
		bool IsLink(ReferenceSegment referenceSegment)
		{
			return referenceSegment.IsLocal || !referenceSegment.IsDefinition;
		}
		#endregion

		#region SaveToDisk
		/// <summary>
		/// Shows the 'save file dialog', prompting the user to save the decompiled nodes to disk.
		/// </summary>
		public void SaveToDisk(ILSpy.Language language, IEnumerable<ILSpyTreeNode> treeNodes, DecompilationOptions options)
		{
			if (!treeNodes.Any())
				return;

			SaveFileDialog dlg = new SaveFileDialog();
			dlg.DefaultExt = language.FileExtension;
			dlg.Filter = language.Name + "|*" + language.FileExtension + Properties.Resources.AllFiles;
			string? nodeText = treeNodes.First().Text?.ToString();
			if (!string.IsNullOrWhiteSpace(nodeText))
			{
				dlg.FileName = WholeProjectDecompiler.CleanUpFileName(nodeText, language.FileExtension);
			}
			if (dlg.ShowDialog() == true)
			{
				SaveToDisk(new DecompilationContext(language, treeNodes.ToArray(), options), dlg.FileName);
			}
		}

		public void SaveToDisk(ILSpy.Language language, IEnumerable<ILSpyTreeNode> treeNodes, DecompilationOptions options, string fileName)
		{
			SaveToDisk(new DecompilationContext(language, treeNodes.ToArray(), options), fileName);
		}

		/// <summary>
		/// Starts the decompilation of the given nodes.
		/// The result will be saved to the given file name.
		/// </summary>
		void SaveToDisk(DecompilationContext context, string fileName)
		{
			RunWithCancellation(
				delegate (CancellationToken ct) {
					context.Options.CancellationToken = ct;
					return SaveToDiskAsync(context, fileName);
				})
				.Then(output => ShowOutput(output))
				.Catch((Exception ex) => {
					textEditor.SyntaxHighlighting = null;
					Debug.WriteLine("Decompiler crashed: " + ex.ToString());
					// Unpack aggregate exceptions as long as there's only a single exception:
					// (assembly load errors might produce nested aggregate exceptions)
					AvalonEditTextOutput output = new AvalonEditTextOutput();
					output.WriteLine(ex.ToString());
					ShowOutput(output);
				}).HandleExceptions();
		}

		Task<AvalonEditTextOutput> SaveToDiskAsync(DecompilationContext context, string fileName)
		{
			TaskCompletionSource<AvalonEditTextOutput> tcs = new TaskCompletionSource<AvalonEditTextOutput>();
			Thread thread = new Thread(new ThreadStart(
				delegate {
					try
					{
						bool originalProjectFormatSetting = context.Options.DecompilerSettings.UseSdkStyleProjectFormat;
						context.Options.EscapeInvalidIdentifiers = true;
						context.Options.Progress = this;
						AvalonEditTextOutput output = new AvalonEditTextOutput {
							EnableHyperlinks = true,
							Title = string.Join(", ", context.TreeNodes.Select(n => n.Text))
						};
						Stopwatch stopwatch = new Stopwatch();
						stopwatch.Start();
						try
						{
							using (StreamWriter w = new StreamWriter(fileName))
							{
								try
								{
									DecompileNodes(context, new PlainTextOutput(w));
								}
								catch (OperationCanceledException)
								{
									w.WriteLine();
									w.WriteLine(Properties.Resources.DecompilationWasCancelled);
									throw;
								}
								catch (PathTooLongException pathTooLong) when (context.Options.SaveAsProjectDirectory != null)
								{
									output.WriteLine(Properties.Resources.ProjectExportPathTooLong, string.Join(", ", context.TreeNodes.Select(n => n.Text)));
									output.WriteLine();
									output.WriteLine(pathTooLong.ToString());
									tcs.SetResult(output);
									return;
								}
							}
						}
						finally
						{
							stopwatch.Stop();
						}

						output.WriteLine(Properties.Resources.DecompilationCompleteInF1Seconds, stopwatch.Elapsed.TotalSeconds);
						if (context.Options.SaveAsProjectDirectory != null)
						{
							output.WriteLine();
							bool useSdkStyleProjectFormat = context.Options.DecompilerSettings.UseSdkStyleProjectFormat;
							if (useSdkStyleProjectFormat)
							{
								output.WriteLine(Properties.Resources.ProjectExportFormatSDKHint);
							}
							else
							{
								output.WriteLine(Properties.Resources.ProjectExportFormatNonSDKHint);
							}
							output.WriteLine(Properties.Resources.ProjectExportFormatChangeSettingHint);
							if (originalProjectFormatSetting != useSdkStyleProjectFormat)
							{
								output.WriteLine(Properties.Resources.CouldNotUseSdkStyleProjectFormat);
							}
						}
						output.WriteLine();
						output.AddButton(null, Properties.Resources.OpenExplorer, delegate { ShellHelper.OpenFolderAndSelectItem(fileName); });
						output.WriteLine();
						tcs.SetResult(output);
					}
					catch (OperationCanceledException)
					{
						tcs.SetCanceled();
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				}));
			thread.Start();
			return tcs.Task;
		}
		#endregion

		#region Clipboard
		private void OnSettingData(object sender, object e)
		{
			// Clipboard handling differs on Avalonia; skip specialized HTML clipboard handling for now.
			// This method is preserved to keep API shape; detailed implementation can be added later.
		}

		private string CreateHtmlFragmentFromSelection()
		{
			var options = new HtmlOptions(textEditor.TextArea.Options);
			var highlighter = textEditor.TextArea.GetService(typeof(IHighlighter)) as IHighlighter;
			var html = new StringBuilder();

			foreach (var segment in textEditor.TextArea.Selection.Segments)
			{
				var line = textEditor.Document.GetLineByOffset(segment.StartOffset);

				while (line != null && line.Offset < segment.EndOffset)
				{
					if (html.Length > 0)
						html.AppendLine("<br>");

					var s = GetOverlap(segment, line);
					var highlightedLine = highlighter?.HighlightLine(line.LineNumber) ?? new HighlightedLine(textEditor.Document, line);

					if (activeRichTextModel is not null)
					{
						var richTextHighlightedLine = new HighlightedLine(textEditor.Document, line);
						foreach (HighlightedSection richTextSection in activeRichTextModel.GetHighlightedSections(s.Offset, s.Length))
							richTextHighlightedLine.Sections.Add(richTextSection);
						highlightedLine.MergeWith(richTextHighlightedLine);
					}

					html.Append(highlightedLine.ToHtml(s.Offset, s.Offset + s.Length, options));
					line = line.NextLine;
				}
			}

			return html.ToString();

			static (int Offset, int Length) GetOverlap(ISegment segment1, ISegment segment2)
			{
				int start = Math.Max(segment1.Offset, segment2.Offset);
				int end = Math.Min(segment1.EndOffset, segment2.EndOffset);
				return (start, end - start);
			}
		}
		#endregion

		internal ReferenceSegment? GetReferenceSegmentAtMousePosition(Point mousePosition)
		{
			if (referenceElementGenerator.References == null)
				return null;
			TextViewPosition? position = GetPositionFromMousePosition(mousePosition);
			if (position == null)
				return null;
			int offset = textEditor.Document.GetOffset(position.Value.Location);
			log.Debug("GetReferenceSegmentAtMousePosition: computed offset {Offset} for Line={Line}, Column={Column}", offset, position.Value.Line, position.Value.Column);
			var seg = referenceElementGenerator.References.FindSegmentsContaining(offset).FirstOrDefault();
			if (seg != null)
			{
				var refText = textEditor.Document.GetText(seg.StartOffset, seg.Length);
				log.Debug("GetReferenceSegmentAtMousePosition: found segment {Type} at {Start}-{End} text=\"{RefText}\"", seg.Reference?.GetType().Name ?? "null", seg.StartOffset, seg.EndOffset, refText);
			}
			return seg;
		}

		internal TextViewPosition? GetPositionFromMousePosition(Point mousePosition)
		{
			log.Debug("GetPositionFromMousePosition: input mouse position = ({X}, {Y})", mousePosition.X, mousePosition.Y);
			
			if (textEditor == null)
			{
				log.Warning("GetPositionFromMousePosition: textEditor is null");
				return null;
			}
			
			var editorPoint = this.TranslatePoint(mousePosition, textEditor);
			if (editorPoint == null)
			{
				log.Warning("GetPositionFromMousePosition: TranslatePoint returned null. Mouse ({X}, {Y}) could not be translated to textEditor coordinates", mousePosition.X, mousePosition.Y);
				log.Debug("DecompilerTextView visual parent: {Parent}, textEditor visual parent: {EditorParent}", 
					this.GetVisualParent()?.GetType().Name ?? "null",
					textEditor.GetVisualParent()?.GetType().Name ?? "null");
				return null;
			}
			
			log.Debug("GetPositionFromMousePosition: translated to editor point = ({X}, {Y})", editorPoint.Value.X, editorPoint.Value.Y);
			log.Debug("TextEditor bounds: Width={Width}, Height={Height}", textEditor.Bounds.Width, textEditor.Bounds.Height);
			
			TextViewPosition? position = textEditor.GetPositionFromPointWithFallback(editorPoint.Value);
			if (position == null)
			{
				log.Warning("GetPositionFromMousePosition: textEditor.GetPositionFromPointWithFallback returned null for editor point ({X}, {Y})", editorPoint.Value.X, editorPoint.Value.Y);
				return null;
			}
			
			log.Debug("GetPositionFromMousePosition: got TextViewPosition Line={Line}, Column={Column}, VisualColumn={VisCol}", 
				position.Value.Line, position.Value.Column, position.Value.VisualColumn);
			// Additional diagnostics: log the document line text and surrounding context
			try
			{
				var docLine = textEditor.Document.GetLineByNumber(position.Value.Line);
				var docLineText = textEditor.Document.GetText(docLine.Offset, docLine.Length);
				int posOffset = textEditor.Document.GetOffset(position.Value.Location);
				int indexInLine = posOffset - docLine.Offset;
				char? charUnder = (indexInLine >= 0 && indexInLine < docLineText.Length) ? docLineText[indexInLine] : (char?)null;
				log.Debug("GetPositionFromMousePosition: docLine {Line} offset={Offset} len={Len} indexInLine={Index} charUnder={Char}", position.Value.Line, docLine.Offset, docLine.Length, indexInLine, charUnder?.ToString() ?? "(none)");
				
				// Log a small excerpt around the line for context
				int startLine = Math.Max(1, position.Value.Line - 2);
				int endLine = Math.Min(textEditor.Document.LineCount, position.Value.Line + 2);
				var sb = new System.Text.StringBuilder();
				for (int ln = startLine; ln <= endLine; ln++)
				{
					var l = textEditor.Document.GetLineByNumber(ln);
					var txt = textEditor.Document.GetText(l.Offset, l.Length).Replace("\t", "\\t");
					sb.AppendLine($"{ln,5}: {txt}");
				}
				log.Debug("GetPositionFromMousePosition: surrounding lines {Start}-{End}:\n{Excerpt}", startLine, endLine, sb.ToString());
				
				// Visual line mapping diagnostics
				var tv = textEditor.TextArea.TextView;
				if (tv != null)
				{
					var visualLine = tv.GetVisualLineFromVisualTop(editorPoint.Value.Y + tv.VerticalOffset);
					if (visualLine != null)
					{
						log.Debug("GetPositionFromMousePosition: visualLine firstDocLine={FirstDocLine} lastDocLine={LastDocLine} visualLinesCount={VisCount}", visualLine.FirstDocumentLine.LineNumber, visualLine.LastDocumentLine.LineNumber, tv.VisualLines.Count);
					}
					else
					{
						log.Debug("GetPositionFromMousePosition: visualLine is null for editorPoint Y={Y}", editorPoint.Value.Y);
					}
				}
			}
			catch (Exception ex)
			{
				log.Debug(ex, "GetPositionFromMousePosition: failed while collecting line/visual diagnostics");
			}
			
			log.Debug("GetPositionFromMousePosition: returning valid position Line={Line}, Column={Column}", position.Value.Line, position.Value.Column);
			return position;
		}

		// Helper to determine identifier characters (letters, digits, underscore, and '.')
		private static bool IsIdentifierChar(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_' || c == '.';
		}

		public DecompilerTextViewState? GetState()
		{
			if (decompiledNodes == null && currentAddress == null)
				return null;

			var state = new DecompilerTextViewState();
			if (foldingManager != null)
				state.SaveFoldingsState(foldingManager.AllFoldings);
			state.VerticalOffset = textEditor.VerticalOffset;
			state.HorizontalOffset = textEditor.HorizontalOffset;
			state.ExpandMemberDefinitions = expandMemberDefinitions;
			state.DecompiledNodes = decompiledNodes == null ? null : new HashSet<ILSpyTreeNode>(decompiledNodes);
			state.ViewedUri = currentAddress;
			return state;
		}

		ViewState? IHaveState.GetState() => GetState();

		public static void RegisterHighlighting()
		{
			HighlightingManager.Instance.RegisterHighlighting("ILAsm", new[] { ".il" }, "ILAsm-Mode");
			HighlightingManager.Instance.RegisterHighlighting("C#", new[] { ".cs" }, "CSharp-Mode");
			HighlightingManager.Instance.RegisterHighlighting("Asm", new[] { ".s", ".asm" }, "Asm-Mode");
			HighlightingManager.Instance.RegisterHighlighting("xml", new[] { ".xml", ".baml" }, "XML-Mode");
		}

		public static void RefreshHighlightingForAllOpenEditors()
		{
			if (Application.Current == null)
				return;
			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				foreach (var w in desktop.Windows)
				{
					foreach (var desc in w.GetVisualDescendants())
					{
						if (desc is DecompilerTextView view)
						{
							// Force re-registration of highlighting by re-invoking RegisterHighlighting and
							// forcing the text editor to re-evaluate its colorizer.
							DecompilerTextView.RegisterHighlighting();
							view.textEditor.TextArea.TextView.Redraw();
						}
					}
				}
			}
		}

		#region Unfold
		public void UnfoldAndScroll(int lineNumber)
		{
			if (lineNumber <= 0 || lineNumber > textEditor.Document.LineCount)
				return;
			if (foldingManager == null)
				return;

			var line = textEditor.Document.GetLineByNumber(lineNumber);

			// unfold
			var foldings = foldingManager.GetFoldingsContaining(line.Offset);
			if (foldings != null)
			{
				foreach (var folding in foldings)
				{
					if (folding.IsFolded)
					{
						folding.IsFolded = false;
					}
				}
			}
			// scroll to
			textEditor.ScrollTo(lineNumber, 0);
		}

		public FoldingManager? FoldingManager {
			get {
				return foldingManager;
			}
		}
		#endregion
	}

	public class DecompilerTextViewState : ViewState
	{
		private List<(int StartOffset, int EndOffset)>? ExpandedFoldings;
		private int FoldingsChecksum;
		public bool ExpandMemberDefinitions;
		public double VerticalOffset;
		public double HorizontalOffset;

		public void SaveFoldingsState(IEnumerable<FoldingSection> foldings)
		{
			ExpandedFoldings = foldings.Where(f => !f.IsFolded)
				.Select(f => (f.StartOffset, f.EndOffset)).ToList();
			FoldingsChecksum = unchecked(foldings.Select(f => f.StartOffset * 3 - f.EndOffset)
				.DefaultIfEmpty()
				.Aggregate((a, b) => a + b));
		}

		internal void RestoreFoldings(List<NewFolding> list, bool expandMemberDefinitions)
		{
			if (ExpandedFoldings == null)
				return;
			var checksum = unchecked(list.Select(f => f.StartOffset * 3 - f.EndOffset)
				.DefaultIfEmpty()
				.Aggregate((a, b) => a + b));
			if (FoldingsChecksum == checksum)
			{
				foreach (var folding in list)
				{
					bool wasExpanded = ExpandedFoldings.Any(
						f => f.StartOffset == folding.StartOffset
							&& f.EndOffset == folding.EndOffset
					);
					bool isExpanded = !folding.DefaultClosed;
					// State of the folding was changed
					if (wasExpanded != isExpanded)
					{
						// The "ExpandMemberDefinitions" setting was not changed
						if (expandMemberDefinitions == ExpandMemberDefinitions)
						{
							// restore fold state
							folding.DefaultClosed = !wasExpanded;
						}
						else
						{
							// only restore fold state if fold was not a definition
							if (!folding.IsDefinition)
							{
								folding.DefaultClosed = !wasExpanded;
							}
						}
					}
				}
			}
		}

		public override bool Equals(ViewState? other)
		{
			if (other is DecompilerTextViewState vs)
			{
				return base.Equals(vs)
					&& FoldingsChecksum == vs.FoldingsChecksum
					&& VerticalOffset == vs.VerticalOffset
					&& HorizontalOffset == vs.HorizontalOffset;
			}
			return false;
		}

		protected override string GetDebuggerDisplay()
		{
			return $"{base.GetDebuggerDisplay()}, ExpandMemberDefinitions = {ExpandMemberDefinitions}, VerticalOffset = {VerticalOffset}, HorizontalOffset = {HorizontalOffset}, FoldingsChecksum = {FoldingsChecksum}";
		}
	}

	static class ExtensionMethods
	{
		private static readonly Serilog.ILogger log = ICSharpCode.ILSpy.Util.LogCategory.For("DecompilerTextView");

		public static void RegisterHighlighting(
			this HighlightingManager manager,
			string name,
			string[] extensions,
			string resourceName)
		{
			Stream? resourceStream = typeof(DecompilerTextView).Assembly
				.GetManifestResourceStream(typeof(DecompilerTextView), resourceName + ".xshd");

			if (resourceStream != null)
			{
				log.Debug("Loading highlighting definition for {Name}", name);
				IHighlightingDefinition highlightingDefinition;

				using (resourceStream)
				using (XmlTextReader reader = new XmlTextReader(resourceStream))
				{
					highlightingDefinition = HighlightingLoader.Load(reader, manager);
				}

				manager.RegisterHighlighting(
				name, extensions,
				delegate {
					ThemeManager.Current.ApplyHighlightingColors(highlightingDefinition);
					return highlightingDefinition;
				});
			}
		}
	}

	// Converter to multiply a double by a factor provided as ConverterParameter
	public class MultiplyConverter : Avalonia.Data.Converters.IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
		{
			if (value is double d && parameter != null)
			{
				if (double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double factor))
				{
					return d * factor;
				}
			}
			return null;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
		{
			throw new NotSupportedException();
		}
	}
}
