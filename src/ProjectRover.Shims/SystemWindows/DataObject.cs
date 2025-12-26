using System;
using System.Collections.Generic;
using System.Windows;

namespace System.Windows
{
    /// <summary>
    /// Minimal shim of WPF's DataObject for portability.
    /// Note: This is intentionally small — only members required by the port are implemented.
    /// </summary>
    public class DataObject : IDataObject
    {
        readonly Dictionary<string, object?> storage = new Dictionary<string, object?>();

        public DataObject()
        {
        }

        public object? GetData(string format)
        {
            storage.TryGetValue(format, out var value);
            return value;
        }

        public bool GetDataPresent(string format)
        {
            return storage.ContainsKey(format);
        }

        public void SetData(string format, object? data)
        {
            storage[format] = data;
        }

        public void SetText(string text)
        {
            SetData(DataFormats.Text, text);
            SetData(DataFormats.UnicodeText, text);
        }

        public void SetText(string text, TextDataFormat format)
        {
            // only handle basic CSV or Plain text as Text
            SetText(text);
        }

        // Pasting handler helpers (no-op for Avalonia port; provided for compile-time compatibility)
        public static void AddPastingHandler(object target, Action<object, DataObjectPastingEventArgs> handler)
        {
            // No-op: Avalonia handles paste differently. Handlers in code will not be invoked.
        }

        public static void AddSettingDataHandler(object target, Action<object, DataObjectSettingDataEventArgs> handler)
        {
            // No-op placeholder for WPF compatibility.
        }
    }
}
