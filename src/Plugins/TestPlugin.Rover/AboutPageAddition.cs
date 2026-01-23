using System.Composition;
using ICSharpCode.ILSpy;

namespace TestPlugin.Rover
{
    [Export(typeof(IAboutPageAddition))]
    [Shared]
    public class AboutPageAddition : IAboutPageAddition
    {
        public void Write(ISmartTextOutput textOutput)
        {
            textOutput.WriteLine();
            textOutput.Write("Rover test plugin says hello.");
            textOutput.WriteLine();
        }
    }
}
