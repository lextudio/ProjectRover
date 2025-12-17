// Converted to Avalonia by automated migration
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;

using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Options;

using ICSharpCode.ILSpy.Views;

namespace ICSharpCode.ILSpy.TextViewControl
{
    /// <summary>
    /// Renders XML documentation into an Avalonia Control (ScrollViewer containing a StackPanel).
    /// This preserves the original public methods used by the rest of the code but returns a Control
    /// instead of a FlowDocument.
    /// </summary>
    public class DocumentationUIBuilder
    {
        readonly IAmbience ambience;
        readonly IHighlightingDefinition highlightingDefinition;
        readonly DisplaySettings displaySettings;
        readonly MainWindow mainWindow;

        readonly StackPanel blockContainer;
        readonly ScrollViewer scrollViewer;

        TextBlock currentParagraph;

        public DocumentationUIBuilder(IAmbience ambience, IHighlightingDefinition highlightingDefinition, DisplaySettings displaySettings, MainWindow mainWindow)
        {
            this.ambience = ambience;
            this.highlightingDefinition = highlightingDefinition;
            this.displaySettings = displaySettings;
            this.mainWindow = mainWindow;

            blockContainer = new StackPanel() { Orientation = Orientation.Vertical };
            scrollViewer = new ScrollViewer { Content = blockContainer };

            this.ShowSummary = true;
            this.ShowAllParameters = true;
            this.ShowReturns = true;
            this.ShowThreadSafety = true;
            this.ShowExceptions = true;
            this.ShowTypeParameters = true;

            this.ShowExample = true;
            this.ShowPreliminary = true;
            this.ShowSeeAlso = true;
            this.ShowValue = true;
            this.ShowPermissions = true;
            this.ShowRemarks = true;
        }

        // Return type changed: callers expect FlowDocument; update call sites to accept Control.
        public Control CreateDocument()
        {
            FlushAddedText(true);
            return scrollViewer;
        }

        public bool ShowExceptions { get; set; }
        public bool ShowPermissions { get; set; }
        public bool ShowExample { get; set; }
        public bool ShowPreliminary { get; set; }
        public bool ShowRemarks { get; set; }
        public bool ShowSummary { get; set; }
        public bool ShowReturns { get; set; }
        public bool ShowSeeAlso { get; set; }
        public bool ShowThreadSafety { get; set; }
        public bool ShowTypeParameters { get; set; }
        public bool ShowValue { get; set; }
        public bool ShowAllParameters { get; set; }

        public void AddCodeBlock(string textContent, bool keepLargeMargin = false)
        {
            var document = new TextDocument(textContent);
            var highlighter = new DocumentHighlighter(document, highlightingDefinition);
            // TODO: var richText = DocumentPrinter.ConvertTextDocumentToRichText(document, highlighter).ToRichTextModel();

            var tb = NewParagraph();
            tb.FontFamily = GetCodeFont();
            if (!keepLargeMargin)
                tb.Margin = new Thickness(0, 6, 0, 6);

            // Simple mapping: append text runs
            //foreach (var run in richText.CreateRuns(document))
            //    tb.Text += run.Text;

            AddBlock(tb);
        }

        public void AddSignatureBlock(string signature, RichTextModel highlighting = null)
        {
            var document = new TextDocument(signature);
            // TODO: var richText = highlighting ?? DocumentPrinter.ConvertTextDocumentToRichText(document, new DocumentHighlighter(document, highlightingDefinition)).ToRichTextModel();
            var tb = NewParagraph();
            tb.FontFamily = GetCodeFont();
            tb.FontSize = displaySettings.SelectedFontSize;
            //foreach (var run in richText.CreateRuns(document))
            //    tb.Text += run.Text;
            AddBlock(tb);
        }

        public void AddXmlDocumentation(string xmlDocumentation, IEntity declaringEntity, Func<string, IEntity> resolver)
        {
            if (xmlDocumentation == null)
                return;
            Debug.WriteLine(xmlDocumentation);
            var xml = XElement.Parse("<doc>" + xmlDocumentation + "</doc>");
            AddDocumentationElement(new XmlDocumentationElement(xml, declaringEntity, resolver));
        }

        public string ParameterName { get; set; }

        public void AddDocumentationElement(XmlDocumentationElement element)
        {
            if (element == null)
                throw new ArgumentNullException("element");
            if (element.IsTextNode)
            {
                AddText(element.TextContent);
                return;
            }
            switch (element.Name)
            {
                case "b":
                    AddSpanBold(element.Children);
                    break;
                case "i":
                    AddSpanItalic(element.Children);
                    break;
                case "c":
                    AddSpanCode(element.Children);
                    break;
                case "code":
                    AddCodeBlock(element.TextContent);
                    break;
                case "example":
                    if (ShowExample)
                        AddSection("Example: ", element.Children);
                    break;
                case "exception":
                    if (ShowExceptions)
                        AddException(element.ReferencedEntity, element.Children);
                    break;
                case "list":
                    AddList(element.GetAttribute("type"), element.Children);
                    break;
                case "para":
                    AddParagraph(element.Children);
                    break;
                case "param":
                    if (ShowAllParameters || (ParameterName != null && ParameterName == element.GetAttribute("name")))
                        AddParam(element.GetAttribute("name"), element.Children);
                    break;
                case "paramref":
                    AddParamRef(element.GetAttribute("name"));
                    break;
                case "permission":
                    if (ShowPermissions)
                        AddPermission(element.ReferencedEntity, element.Children);
                    break;
                case "preliminary":
                    if (ShowPreliminary)
                        AddPreliminary(element.Children);
                    break;
                case "remarks":
                    if (ShowRemarks)
                        AddSection("Remarks: ", element.Children);
                    break;
                case "returns":
                    if (ShowReturns)
                        AddSection("Returns: ", element.Children);
                    break;
                case "see":
                    AddSee(element);
                    break;
                case "seealso":
                    if (currentParagraph != null)
                        AddSee(element);
                    else if (ShowSeeAlso)
                        AddSection("See also: ", element.Children);
                    break;
                case "summary":
                    if (ShowSummary)
                        AddSection("Summary: ", element.Children);
                    break;
                case "threadsafety":
                    if (ShowThreadSafety)
                        AddThreadSafety(ParseBool(element.GetAttribute("static")), ParseBool(element.GetAttribute("instance")), element.Children);
                    break;
                case "typeparam":
                    if (ShowTypeParameters)
                        AddSection("Type parameter " + element.GetAttribute("name") + ": ", element.Children);
                    break;
                case "typeparamref":
                    AddText(element.GetAttribute("name"));
                    break;
                case "value":
                    if (ShowValue)
                        AddSection("Value: ", element.Children);
                    break;
                case "br":
                    AddLineBreak();
                    break;
                default:
                    foreach (var child in element.Children)
                        AddDocumentationElement(child);
                    break;
            }
        }

        void AddList(string type, IEnumerable<XmlDocumentationElement> items)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            AddBlock(panel);
            foreach (var itemElement in items)
            {
                if (itemElement.Name == "listheader" || itemElement.Name == "item")
                {
                    var itemPanel = new StackPanel();// { Orientation = Orientation.Horizontal };
                    var marker = new TextBlock { Text = "• ", Width = 20 };
                    itemPanel.Children.Add(marker);
                    var contentPanel = new StackPanel();// { Orientation = Orientation.Vertical };
                    itemPanel.Children.Add(contentPanel);
                    var oldContainer = blockContainer;
                    try
                    {
                        // temporarily use contentPanel as blockContainer to add children
                        blockContainer.Children.Add(itemPanel);
                        currentParagraph = null;
                        foreach (var prop in itemElement.Children)
                        {
                            AddDocumentationElement(prop);
                        }
                        FlushAddedText(false);
                    }
                    finally
                    {
                        currentParagraph = null;
                    }
                }
            }
        }

        bool? ParseBool(string input)
        {
            if (bool.TryParse(input, out bool result))
                return result;
            else
                return null;
        }

        void AddThreadSafety(bool? staticThreadSafe, bool? instanceThreadSafe, IEnumerable<XmlDocumentationElement> children)
        {
            AddSection("Thread-safety: ", delegate {
                if (staticThreadSafe == true)
                    AddText("Any public static members of this type are thread safe. ");
                else if (staticThreadSafe == false)
                    AddText("The static members of this type are not thread safe. ");

                if (instanceThreadSafe == true)
                    AddText("Any public instance members of this type are thread safe. ");
                else if (instanceThreadSafe == false)
                    AddText("Any instance members are not guaranteed to be thread safe. ");

                foreach (var child in children)
                    AddDocumentationElement(child);
            });
        }

        void AddException(IEntity referencedEntity, IList<XmlDocumentationElement> children)
        {
            var span = NewParagraph();
            if (referencedEntity != null)
                span.Text += ambience.ConvertSymbol(referencedEntity) + ": ";
            else
                span.Text += "Exception: ";
            AddBlock(span);
            foreach (var child in children)
                AddDocumentationElement(child);
        }

        void AddPermission(IEntity referencedEntity, IList<XmlDocumentationElement> children)
        {
            var span = NewParagraph();
            span.Text += "Permission";
            if (referencedEntity != null)
            {
                span.Text += " ";
                span.Text += ambience.ConvertSymbol(referencedEntity);
            }
            span.Text += ": ";
            AddBlock(span);
            foreach (var child in children)
                AddDocumentationElement(child);
        }

        void AddParam(string name, IEnumerable<XmlDocumentationElement> children)
        {
            var span = NewParagraph();
            span.Text += name ?? string.Empty;
            AddBlock(span);
            foreach (var child in children)
                AddDocumentationElement(child);
        }

        void AddParamRef(string name)
        {
            if (name != null)
            {
                AddInlineText(name);
            }
        }

        void AddPreliminary(IEnumerable<XmlDocumentationElement> children)
        {
            if (children.Any())
            {
                foreach (var child in children)
                    AddDocumentationElement(child);
            }
            else
            {
                AddText("[This is preliminary documentation and subject to change.]");
            }
        }

        void AddSee(XmlDocumentationElement element)
        {
            var referencedEntity = element.ReferencedEntity;
            if (referencedEntity != null)
            {
                if (element.Children.Any())
                {
                    AddSpanBold(element.Children);
                }
                else
                {
                    AddInlineText(ambience.ConvertSymbol(referencedEntity));
                }
            }
            else if (element.GetAttribute("langword") != null)
            {
                AddInlineText(element.GetAttribute("langword"));
            }
            else if (element.GetAttribute("href") != null)
            {
                AddInlineText(element.GetAttribute("href"));
            }
            else
            {
                AddText(element.GetAttribute("cref"));
            }
        }

        FontFamily GetCodeFont()
        {
            return displaySettings.SelectedFont;
        }

        TextBlock NewParagraph(double marginTop = 0, double marginBottom = 5)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, marginTop, 0, marginBottom),
                FontSize = displaySettings.SelectedFontSize,
                FontFamily = displaySettings.SelectedFont
            };
            return tb;
        }

        void AddInlineText(string text)
        {
            EnsureParagraph();
            currentParagraph.Text += text;
        }

        void AddText(string textContent)
        {
            if (string.IsNullOrEmpty(textContent))
                return;
            // Basic whitespace handling similar to original
            for (int i = 0; i < textContent.Length; i++)
            {
                char c = textContent[i];
                if (c == '\n' && IsEmptyLineBefore(textContent, i))
                {
                    AddLineBreak();
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (currentParagraph == null || currentParagraph.Text.EndsWith(" "))
                        continue;
                    AddInlineText(" ");
                }
                else
                {
                    AddInlineText(c.ToString());
                }
            }
        }

        bool IsEmptyLineBefore(string text, int i)
        {
            do { i--; } while (i >= 0 && (text[i] == ' ' || text[i] == '\r'));
            return i >= 0 && text[i] == '\n';
        }

        void AddLineBreak()
        {
            FlushAddedText();
            // add empty paragraph
            AddBlock(NewParagraph());
        }

        void AddSection(string title, IEnumerable<XmlDocumentationElement> children)
        {
            AddSection(title, () => {
                foreach (var child in children)
                    AddDocumentationElement(child);
            });
        }

        void AddSection(string title, Action addChildren)
        {
            var header = NewParagraph();
            header.FontWeight = FontWeight.Bold;
            header.Text = title;
            AddBlock(header);
            addChildren();
        }

        void AddParagraph(IEnumerable<XmlDocumentationElement> children)
        {
            EnsureParagraph();
            foreach (var child in children)
                AddDocumentationElement(child);
            FlushAddedText(false);
        }

        void AddSpanBold(IEnumerable<XmlDocumentationElement> children)
        {
            EnsureParagraph();
            // naive: wrap children text with ** markers
            AddInlineText("*");
            foreach (var child in children)
                AddDocumentationElement(child);
            AddInlineText("*");
        }

        void AddSpanItalic(IEnumerable<XmlDocumentationElement> children)
        {
            EnsureParagraph();
            AddInlineText("_");
            foreach (var child in children)
                AddDocumentationElement(child);
            AddInlineText("_");
        }

        void AddSpanCode(IEnumerable<XmlDocumentationElement> children)
        {
            EnsureParagraph();
            AddInlineText("`");
            foreach (var child in children)
                AddDocumentationElement(child);
            AddInlineText("`");
        }

        void EnsureParagraph()
        {
            if (currentParagraph == null)
            {
                currentParagraph = NewParagraph();
                AddBlock(currentParagraph);
            }
        }

        void AddBlock(Control block)
        {
            FlushAddedText(true);
            blockContainer.Children.Add(block);
            currentParagraph = null;
        }

        StringBuilder addedText = new StringBuilder();

        void FlushAddedText(bool trim = true)
        {
            if (addedText.Length == 0) return;
            var text = addedText.ToString();
            addedText.Clear();
            EnsureParagraph();
            currentParagraph.Text += text;
        }

        void FlushAddedText()
        {
            FlushAddedText(true);
        }
    }
}
