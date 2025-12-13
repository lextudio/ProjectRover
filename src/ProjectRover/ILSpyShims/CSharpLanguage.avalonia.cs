using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaEdit.Highlighting;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.TreeNodes;

namespace ICSharpCode.ILSpy
{
    public partial class CSharpLanguage
    {
        void AddWarningMessage(MetadataFile module, ITextOutput output, string line1, string line2 = null,
        string buttonText = null, Avalonia.Media.Imaging.Bitmap buttonImage = null, EventHandler<RoutedEventArgs> buttonClickHandler = null)
        {
            if (output is ISmartTextOutput fancyOutput)
            {
                string text = line1;
                if (!string.IsNullOrEmpty(line2))
                    text += Environment.NewLine + line2;
                fancyOutput.AddUIElement(() => new StackPanel {
                    Margin = new Thickness(5),
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new Image {
                            Width = 32,
                            Height = 32,
                            Source = Images.Load(this, "Images/Warning")
                        },
                        new TextBlock {
                            Margin = new Thickness(5, 0, 0, 0),
                            Text = text
                        }
                    }
                });
                fancyOutput.WriteLine();
                if (buttonText != null && buttonClickHandler != null)
                {
                    fancyOutput.AddButton(buttonImage, buttonText, buttonClickHandler);
                    fancyOutput.WriteLine();
                }
            }
            else
            {
                WriteCommentLine(output, line1);
                if (!string.IsNullOrEmpty(line2))
                    WriteCommentLine(output, line2);
            }
        }

        void AddReferenceWarningMessage(MetadataFile module, ITextOutput output)
		{
            // TODO: 
			// var loadedAssembly = AssemblyTreeModel.AssemblyList.GetAssemblies().FirstOrDefault(la => la.GetMetadataFileOrNull() == module);
			// if (loadedAssembly == null || !loadedAssembly.LoadedAssemblyReferencesInfo.HasErrors)
			// 	return;
			// string line1 = Properties.Resources.WarningSomeAssemblyReference;
			// string line2 = Properties.Resources.PropertyManuallyMissingReferencesListLoadedAssemblies;
			// AddWarningMessage(module, output, line1, line2, Properties.Resources.ShowAssemblyLoad, Images.ViewCode, delegate {
			// 	ILSpyTreeNode assemblyNode = AssemblyTreeModel.FindTreeNode(module);
			// 	assemblyNode.EnsureLazyChildren();
			// 	AssemblyTreeModel.SelectNode(assemblyNode.Children.OfType<ReferenceFolderTreeNode>().Single());
			// });
		}

        public override RichText GetRichTextTooltip(IEntity entity)
		{
			var flags = ConversionFlags.All & ~(ConversionFlags.ShowBody | ConversionFlags.PlaceReturnTypeAfterParameterList);
			var output = new StringWriter();
			var decoratedWriter = new TextWriterTokenWriter(output);
			var writer = new CSharpHighlightingTokenWriter(TokenWriter.InsertRequiredSpaces(decoratedWriter), locatable: decoratedWriter);
			var settings = SettingsService.DecompilerSettings.Clone();
			if (!Enum.TryParse(AssemblyTreeModel.CurrentLanguageVersion?.Version, out Decompiler.CSharp.LanguageVersion languageVersion))
				languageVersion = Decompiler.CSharp.LanguageVersion.Latest;
			settings.SetLanguageVersion(languageVersion);
			if (!settings.LiftNullables)
			{
				flags &= ~ConversionFlags.UseNullableSpecifierForValueTypes;
			}
			if (settings.RecordClasses)
			{
				flags |= ConversionFlags.SupportRecordClasses;
			}
			if (settings.RecordStructs)
			{
				flags |= ConversionFlags.SupportRecordStructs;
			}
			if (settings.UnsignedRightShift)
			{
				flags |= ConversionFlags.SupportUnsignedRightShift;
			}
			if (settings.CheckedOperators)
			{
				flags |= ConversionFlags.SupportOperatorChecked;
			}
			if (settings.InitAccessors)
			{
				flags |= ConversionFlags.SupportInitAccessors;
			}
			if (settings.IntroducePrivateProtectedAccessibility)
			{
				flags |= ConversionFlags.UsePrivateProtectedAccessibility;
			}
			if (entity is IMethod m && m.IsLocalFunction)
			{
				writer.WriteIdentifier(Identifier.Create("(local)"));
			}
			new CSharpAmbience() {
				ConversionFlags = flags,
			}.ConvertSymbol(entity, writer, settings.CSharpFormattingOptions);
			return new RichText(output.ToString(), writer.HighlightingModel);
		}
    }
}
