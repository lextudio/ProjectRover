// Avalonia shim for DockLayoutSettings.
// Keeps the same public behavior as the WPF version but avoids a hard
// dependency on AvalonDock types by accepting a generic serializer object
// and invoking Deserialize/Serialize via reflection when available.
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;

namespace ICSharpCode.ILSpy.Docking
{
    public class DockLayoutSettings
    {

        private string? rawSettings;

        public bool Valid => rawSettings != null;

        public void Reset()
        {
            //this.rawSettings = DefaultLayout;
        }

        public DockLayoutSettings(XElement? element)
        {
            if ((element != null) && element.HasElements)
            {
                rawSettings = element.Elements().FirstOrDefault()?.ToString();
            }
        }

        public XElement? SaveAsXml()
        {
            try
            {
                return rawSettings == null ? null : XElement.Parse(rawSettings);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Accept a generic serializer object (Avalonia or other). If the serializer
        // exposes a Deserialize(TextReader) method, call it; otherwise do nothing.
        public void Deserialize(object? serializer)
        {
            if (!Valid)
                //rawSettings = DefaultLayout;
            try
            {
                DeserializeInternal(rawSettings!);
            }
            catch (Exception)
            {
                //DeserializeInternal(DefaultLayout);
            }

            void DeserializeInternal(string settings)
            {
                using (StringReader reader = new StringReader(settings))
                {
                    if (serializer != null)
                    {
                        var mi = serializer.GetType().GetMethod("Deserialize", new Type[] { typeof(TextReader) })
                                 ?? serializer.GetType().GetMethod("Deserialize", new Type[] { typeof(Stream) });
                        if (mi != null)
                        {
                            if (mi.GetParameters()[0].ParameterType == typeof(Stream))
                            {
                                using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(settings));
                                mi.Invoke(serializer, new object[] { ms });
                            }
                            else
                            {
                                mi.Invoke(serializer, new object[] { reader });
                            }
                        }
                    }
                }
            }
        }

        // Accept a generic serializer object. If it exposes Serialize(TextWriter)
        // or Serialize(Stream) we'll call it via reflection and capture the result.
        public void Serialize(object? serializer)
        {
            if (serializer == null)
                return;

            var mi = serializer.GetType().GetMethod("Serialize", new Type[] { typeof(TextWriter) })
                     ?? serializer.GetType().GetMethod("Serialize", new Type[] { typeof(Stream) });
            if (mi != null)
            {
                if (mi.GetParameters()[0].ParameterType == typeof(Stream))
                {
                    using var ms = new MemoryStream();
                    mi.Invoke(serializer, new object[] { ms });
                    ms.Position = 0;
                    using var sr = new StreamReader(ms);
                    rawSettings = sr.ReadToEnd();
                }
                else
                {
                    using var sw = new StringWriter();
                    mi.Invoke(serializer, new object[] { sw });
                    rawSettings = sw.ToString();
                }
            }
        }
    }
}
