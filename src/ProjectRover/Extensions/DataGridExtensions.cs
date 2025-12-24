using System.Windows.Threading;

namespace ICSharpCode.ILSpy
{
    public static class DataGridExtensions
    {
        extension(DataGrid dataGrid)
        {
            public Dispatcher Dispatcher => Dispatcher.CurrentDispatcher;
        }
    }
}
