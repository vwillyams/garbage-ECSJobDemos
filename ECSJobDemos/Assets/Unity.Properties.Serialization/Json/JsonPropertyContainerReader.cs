using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.Properties.Serialization
{
    public static class JsonPropertyContainerReader
    {
        public static TContainer Read<TContainer>(string json)
            where TContainer : IPropertyContainer, new()
        {
            object obj;
            if (!SimpleJson.TryDeserializeObject(json, out obj))
            {
                return new TContainer();
            }
            
            return (TContainer) ParseObject(typeof(TContainer), obj);
        }

        private static object ParseObject(Type type, object obj)
        {
            var instance = Activator.CreateInstance(type);

            if (typeof(IList).IsAssignableFrom(type))
            {
                var enumerable = obj as IEnumerable;

                if (null == enumerable)
                {
                    return instance;
                }

                var list = instance as IList;
                var itemType = type.GetGenericArguments()[0];

                foreach (var item in enumerable)
                {
                    list?.Add(ParseValue(itemType, item));
                }
            }
            else if (typeof(IPropertyContainer).IsAssignableFrom(type))
            {
                var dictionary = obj as IDictionary<string, object>;

                if (null == dictionary)
                {
                    return instance;
                }

                var container = instance as IPropertyContainer;

                foreach (var kvp in dictionary)
                {
                    var property = container.PropertyBag.FindProperty(kvp.Key);

                    var value = ParseValue(property.PropertyType, kvp.Value);

                    if (property is IListProperty)
                    {
                        var list = value as IList;
                        foreach (var item in list)
                        {
                            (property as IListProperty).AddObject(container, item);
                        }
                    }
                    else if (!property.IsReadOnly)
                    {
                        property.SetObjectValue(ref container, value);
                    }
                }

                instance = container;
            }

            return instance;
        }

        private static object ParseValue(Type type, object value)
        {
            var typeCode = Type.GetTypeCode(type);

            if (type.IsEnum)
            {
                return (int) (long) value;
            }

            switch (typeCode)
            {
                case TypeCode.Empty:
                    return null;
                case TypeCode.Object:
                    return ParseObject(type, value);
                case TypeCode.DBNull:
                    return null;
                case TypeCode.Boolean:
                    return (bool) value;
                case TypeCode.Char:
                    return (char) value;
                case TypeCode.SByte:
                    return (sbyte) value;
                case TypeCode.Byte:
                    return (byte) value;
                case TypeCode.Int16:
                    return (short) value;
                case TypeCode.UInt16:
                    return (ushort) value;
                case TypeCode.Int32:
                    return (int) value;
                case TypeCode.UInt32:
                    return (uint) value;
                case TypeCode.Int64:
                    return (long) value;
                case TypeCode.UInt64:
                    return (ulong) value;
                case TypeCode.Single:
                    return (float) value;
                case TypeCode.Double:
                    return (double) value;
                case TypeCode.Decimal:
                    return (decimal) value;
                case TypeCode.DateTime:
                    return (DateTime) value;
                case TypeCode.String:
                    return (string) value;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}