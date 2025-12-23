using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Dock.Model.Core;

namespace ICSharpCode.ILSpy.Docking
{
    internal sealed class DockLayoutXmlSerializer : IDockSerializer
    {
        private readonly DataContractSerializerSettings _settings;
        private readonly Type _listType;

        public DockLayoutXmlSerializer(Type listType, params Type[] knownTypes)
        {
            _listType = listType;
            _settings = new DataContractSerializerSettings
            {
                PreserveObjectReferences = true,
                KnownTypes = knownTypes
            };
        }

        public DockLayoutXmlSerializer()
            : this(typeof(ObservableCollection<>), Array.Empty<Type>())
        {
        }

        public string Serialize<T>(T value)
        {
            var serializer = CreateSerializer(typeof(T));
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = true
            }))
            {
                serializer.WriteObject(writer, value);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public T? Deserialize<T>(string text)
        {
            var serializer = CreateSerializer(typeof(T));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var reader = XmlReader.Create(stream);
            var result = (T?)serializer.ReadObject(reader);
            DockLayoutListTypeConverter.Convert(result, _listType);
            return result;
        }

        public T? Load<T>(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = reader.ReadToEnd();
            return Deserialize<T>(text);
        }

        public void Save<T>(Stream stream, T value)
        {
            var text = Serialize(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(text);
        }

        private DataContractSerializer CreateSerializer(Type type)
        {
            return new DataContractSerializer(type, _settings);
        }
    }

    internal static class DockLayoutListTypeConverter
    {
        public static void Convert(object? obj, Type listType)
        {
            if (obj is null)
                return;

            if (obj is IEnumerable enumerable && obj is not string)
            {
                foreach (var item in enumerable)
                {
                    Convert(item, listType);
                }
            }

            var type = obj.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;

                var value = property.GetValue(obj);
                if (value is null)
                    continue;

                var propType = property.PropertyType;
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    var elementType = propType.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(listType.MakeGenericType(elementType))!;
                    foreach (var item in (IEnumerable)value)
                    {
                        list.Add(item);
                    }

                    foreach (var item in list)
                    {
                        Convert(item, listType);
                    }

                    property.SetValue(obj, list);
                }
                else
                {
                    Convert(value, listType);
                }
            }
        }
    }
}
