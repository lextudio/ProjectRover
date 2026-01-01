using System;

namespace System.Windows
{
    /// <summary>
    /// Minimal shim of WPF's <c>IDataObject</c> used by the port.
    /// Only implements the members referenced by the ILSpy port.
    /// </summary>
    public interface IDataObject
    {
        object? GetData(string format);

        bool GetDataPresent(string format);

        void SetData(string format, object? data);
    }
}
