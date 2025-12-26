using System;

namespace System.Windows
{
    public sealed class DataObjectPastingEventArgs : EventArgs
    {
        public IDataObject SourceDataObject { get; }
        public bool IsDragDrop { get; }
        public string Format { get; }
        public bool Cancel { get; private set; }

        public DataObjectPastingEventArgs(IDataObject dataObject, bool isDragDrop, string format)
        {
            SourceDataObject = dataObject;
            IsDragDrop = isDragDrop;
            Format = format;
        }

        public void CancelCommand()
        {
            Cancel = true;
        }
    }

    public sealed class DataObjectSettingDataEventArgs : EventArgs
    {
        public IDataObject DataObject { get; }
        public string Format { get; }

        public DataObjectSettingDataEventArgs(IDataObject dataObject, string format)
        {
            DataObject = dataObject;
            Format = format;
        }
    }

    public enum TextDataFormat
    {
        Text,
        UnicodeText,
        CommaSeparatedValue,
        Html
    }

    public static class DataFormats
    {
        public const string Text = "Text";
        public const string UnicodeText = "UnicodeText";
        public const string FileDrop = "FileDrop";
        public const string Html = "HTML";
        public const string CommaSeparatedValue = "Csv";
    }
}
