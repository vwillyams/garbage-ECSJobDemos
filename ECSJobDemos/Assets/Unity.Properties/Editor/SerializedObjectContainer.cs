using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Properties.Editor
{
    public class SerializedObjectContainer : IPropertyContainer, IVersionStorage
    {
        private int _version;
        private readonly PropertyBag _tree;
        private string _name;
        private Type _type;

        public SerializedObjectContainer(SerializedObject obj, bool onlyVisible = false)
            : this(obj.GetIterator(), onlyVisible)
        {
            Assert.IsNotNull(obj.targetObject);
            _name = obj.targetObject.name;
            _type = obj.targetObject.GetType();
        }

        private SerializedObjectContainer(SerializedProperty property, bool onlyVisible)
        {
            Assert.IsTrue(property.propertyType == SerializedPropertyType.Generic);
            
            _tree = new PropertyBag();
            for (var enterChildren = true; onlyVisible ? property.NextVisible(enterChildren) : property.Next(enterChildren); enterChildren = false)
            {
                var wrapped = CreateProperty(property, onlyVisible);
                if (wrapped != null)
                {
                    _tree.AddProperty(wrapped);
                }
            }
        }
        
        public int GetVersion<TContainer>(IProperty property, ref TContainer container) 
            where TContainer : IPropertyContainer
        {
            return _version;
        }

        public void IncrementVersion<TContainer>(IProperty property, ref TContainer container)
            where TContainer : IPropertyContainer
        {
            ++_version;
        }

        public IPropertyBag PropertyBag => _tree;

        public string Name => _name;
        public Type Type => _type;

        public IVersionStorage VersionStorage => this;

        public SerializedProperty this[string name] => ((IBaseSerializedProperty) _tree.FindProperty(name)).Property;
        
        private static IBaseSerializedProperty CreateProperty(SerializedProperty p, bool onlyVisible)
        {
            p = p.Copy();
            
            switch (p.propertyType)
            {
                case SerializedPropertyType.AnimationCurve:
                    return new BaseSerializedProperty<AnimationCurve>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].animationCurveValue, 
                        (ref SerializedObjectContainer c, AnimationCurve v) => c[p.name].animationCurveValue = v);
                case SerializedPropertyType.ArraySize:
                    return null;
                case SerializedPropertyType.Boolean:
                    return new BaseSerializedProperty<bool>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].boolValue, 
                        (ref SerializedObjectContainer c, bool v) => c[p.name].boolValue = v);
                case SerializedPropertyType.Bounds:
                    return new BaseSerializedProperty<Bounds>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].boundsValue, 
                        (ref SerializedObjectContainer c, Bounds v) => c[p.name].boundsValue = v);
                case SerializedPropertyType.BoundsInt:
                    return new BaseSerializedProperty<BoundsInt>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].boundsIntValue, 
                        (ref SerializedObjectContainer c, BoundsInt v) => c[p.name].boundsIntValue = v);
                case SerializedPropertyType.Character:
                    return new BaseSerializedProperty<char>(p, 
                        (ref SerializedObjectContainer c) => (char)c[p.name].intValue, 
                        (ref SerializedObjectContainer c, char v) => c[p.name].intValue = v);
                case SerializedPropertyType.Color:
                    return new BaseSerializedProperty<Color>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].colorValue, 
                        (ref SerializedObjectContainer c, Color v) => c[p.name].colorValue = v);
                case SerializedPropertyType.Enum:
                    return new SerializedEnumProperty(p);
                case SerializedPropertyType.ExposedReference:
                    return new BaseSerializedProperty<UnityEngine.Object>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].exposedReferenceValue, 
                        (ref SerializedObjectContainer c, UnityEngine.Object v) => c[p.name].exposedReferenceValue = v);
                case SerializedPropertyType.FixedBufferSize:
                    return null;
                case SerializedPropertyType.Float:
                    if (p.type == "float")
                        return new BaseSerializedProperty<float>(p, 
                            (ref SerializedObjectContainer c) => c[p.name].floatValue, 
                            (ref SerializedObjectContainer c, float v) => c[p.name].floatValue = v);
                    else
                        return new BaseSerializedProperty<double>(p, 
                            (ref SerializedObjectContainer c) => c[p.name].doubleValue, 
                            (ref SerializedObjectContainer c, double v) => c[p.name].doubleValue = v);
                case SerializedPropertyType.Generic:
                    if (p.isArray)
                    {
                        // if the array elements are primitives, use a primitive list (otherwise return null)
                        return CreatePrimitiveListProperty(p.Copy());
                    }
                    if (p.isFixedBuffer)
                    {
                        // TODO
                        return null;
                    }
                    var container = new SerializedObjectContainer(p.Copy(), onlyVisible);
                    return new SerializedStructProperty(p, (ref SerializedObjectContainer c) => container);
                case SerializedPropertyType.Gradient:
                    return new BaseSerializedProperty<Gradient>(p, 
                        (ref SerializedObjectContainer c) => SafeGradientValue(c[p.name]), 
                        (ref SerializedObjectContainer c, Gradient v) => SafeGradientValue(c[p.name], v));
                case SerializedPropertyType.Integer:
                    switch (p.type)
                    {
                        case "byte": return new BaseSerializedProperty<byte>(p, 
                            (ref SerializedObjectContainer c) => (byte)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, byte v) => c[p.name].intValue = v);
                        case "sbyte": return new BaseSerializedProperty<sbyte>(p, 
                            (ref SerializedObjectContainer c) => (sbyte)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, sbyte v) => c[p.name].intValue = v);
                        case "char": return new BaseSerializedProperty<char>(p, 
                            (ref SerializedObjectContainer c) => (char)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, char v) => c[p.name].intValue = v);
                        case "ushort": return new BaseSerializedProperty<ushort>(p, 
                            (ref SerializedObjectContainer c) => (ushort)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, ushort v) => c[p.name].intValue = v);
                        case "short": return new BaseSerializedProperty<short>(p, 
                            (ref SerializedObjectContainer c) => (short)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, short v) => c[p.name].intValue = v);
                        case "int": return new BaseSerializedProperty<int>(p, 
                            (ref SerializedObjectContainer c) => c[p.name].intValue, 
                            (ref SerializedObjectContainer c, int v) => c[p.name].intValue = v);
                        case "uint": return new BaseSerializedProperty<uint>(p, 
                            (ref SerializedObjectContainer c) => (uint)c[p.name].intValue, 
                            (ref SerializedObjectContainer c, uint v) => c[p.name].intValue = (int)v);
                        case "long": return new BaseSerializedProperty<long>(p, 
                            (ref SerializedObjectContainer c) => c[p.name].longValue, 
                            (ref SerializedObjectContainer c, long v) => c[p.name].longValue = v);
                        case "ulong": return new BaseSerializedProperty<ulong>(p, 
                            (ref SerializedObjectContainer c) => (ulong)c[p.name].longValue, 
                            (ref SerializedObjectContainer c, ulong v) => c[p.name].longValue = (long)v);
                        default: throw new Exception("Unknown SerializedPropertyType.Integer type: " + p.type);
                    }
                case SerializedPropertyType.LayerMask:
                    return new BaseSerializedProperty<LayerMask>(p, 
                        (ref SerializedObjectContainer c) => (LayerMask)c[p.name].intValue, 
                        (ref SerializedObjectContainer c, LayerMask v) => c[p.name].intValue = v);
                case SerializedPropertyType.ObjectReference:
                    return new BaseSerializedProperty<UnityEngine.Object>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].objectReferenceValue, 
                        (ref SerializedObjectContainer c, UnityEngine.Object v) => c[p.name].objectReferenceValue = v);
                case SerializedPropertyType.Quaternion:
                    return new BaseSerializedProperty<Quaternion>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].quaternionValue, 
                        (ref SerializedObjectContainer c, Quaternion v) => c[p.name].quaternionValue = v);
                case SerializedPropertyType.Rect:
                    return new BaseSerializedProperty<Rect>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].rectValue, 
                        (ref SerializedObjectContainer c, Rect v) => c[p.name].rectValue = v);
                case SerializedPropertyType.RectInt:
                    return new BaseSerializedProperty<RectInt>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].rectIntValue, 
                        (ref SerializedObjectContainer c, RectInt v) => c[p.name].rectIntValue = v);
                case SerializedPropertyType.String:
                    return new BaseSerializedProperty<string>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].stringValue, 
                        (ref SerializedObjectContainer c, string v) => c[p.name].stringValue = v);
                case SerializedPropertyType.Vector2:
                    return new BaseSerializedProperty<Vector2>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].vector2Value, 
                        (ref SerializedObjectContainer c, Vector2 v) => c[p.name].vector2Value = v);
                case SerializedPropertyType.Vector2Int:
                    return new BaseSerializedProperty<Vector2Int>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].vector2IntValue, 
                        (ref SerializedObjectContainer c, Vector2Int v) => c[p.name].vector2IntValue = v);
                case SerializedPropertyType.Vector3:
                    return new BaseSerializedProperty<Vector3>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].vector3Value, 
                        (ref SerializedObjectContainer c, Vector3 v) => c[p.name].vector3Value = v);
                case SerializedPropertyType.Vector3Int:
                    return new BaseSerializedProperty<Vector3Int>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].vector3IntValue, 
                        (ref SerializedObjectContainer c, Vector3Int v) => c[p.name].vector3IntValue = v);
                case SerializedPropertyType.Vector4:
                    return new BaseSerializedProperty<Vector4>(p, 
                        (ref SerializedObjectContainer c) => c[p.name].vector4Value, 
                        (ref SerializedObjectContainer c, Vector4 v) => c[p.name].vector4Value = v);
                default:
                    throw new Exception("Unknown property type: " + p.propertyType);
            }
        }

        // https://gist.github.com/capnslipp/8516384
        /// Access to SerializedProperty's internal gradientValue property getter, in a manner that'll only soft break (returning null) if the property changes or disappears in future Unity revs.
        private static Gradient SafeGradientValue(SerializedProperty sp)
        {
            BindingFlags instanceAnyPrivacyBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty(
                "gradientValue",
                instanceAnyPrivacyBindingFlags,
                null,
                typeof(Gradient),
                new Type[0],
                null
            );
            if (propertyInfo == null)
                return null;

            Gradient gradientValue = propertyInfo.GetValue(sp, null) as Gradient;
            return gradientValue;
        }

        private static void SafeGradientValue(SerializedProperty sp, Gradient gradient)
        {
            BindingFlags instanceAnyPrivacyBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty(
                "gradientValue",
                instanceAnyPrivacyBindingFlags,
                null,
                typeof(Gradient),
                new Type[0],
                null
            );
            if (propertyInfo == null)
                return;

            propertyInfo.SetValue(sp, gradient);
        }

        private class BaseSerializedProperty<TValue> : Property<SerializedObjectContainer, TValue>, IBaseSerializedProperty
        {
            public SerializedProperty Property { get; }

            public BaseSerializedProperty(SerializedProperty property, GetValueMethod getter, SetValueMethod setter)
                : base(property.name, getter, setter)
            {
                Property = property;
            }
        }

        private class SerializedStructProperty : BaseSerializedProperty<SerializedObjectContainer>
        {
            public SerializedStructProperty(SerializedProperty property, GetValueMethod getter)
                : base(property, getter, null)
            {
            }

            public override void Accept(ref SerializedObjectContainer container, IPropertyVisitor visitor)
            {
                var value = GetValue(ref container);

                var typedVisitor = visitor as IPropertyVisitor<SerializedObjectContainer>;

                if (null != typedVisitor)
                {
                    typedVisitor.Visit(ref container, new VisitContext<SerializedObjectContainer> {Property = this, Value = value, Index = -1});
                }
                else
                {
                    var subtreeContext = new SubtreeContext<SerializedObjectContainer> {Property = this, Value = value, Index = -1};
                    if (visitor.BeginSubtree(ref container, subtreeContext))
                    {
                        value.PropertyBag.Visit(ref value, visitor);
                    }
                    visitor.EndSubtree(ref container, subtreeContext);
                }
            }
        }
        
        public struct EnumValue
        {
            public int valueIndex;
        }
        
        public interface ISerializedPropertyVisitor : IBuiltInPropertyVisitor,
            IPropertyVisitor<EnumValue>
        {}
        
        private class SerializedEnumProperty : BaseSerializedProperty<EnumValue>
        {
            public SerializedEnumProperty(SerializedProperty p)
                : base(p, (ref SerializedObjectContainer c) => new EnumValue() {valueIndex = c[p.name].enumValueIndex}, (ref SerializedObjectContainer c, EnumValue v) => c[p.name].enumValueIndex = v.valueIndex)
            {
            }
        }
        
        private class SerializedListProperty<TValue> : ListProperty<SerializedObjectContainer, SerializedList<TValue>, TValue>,
            IBaseSerializedProperty
            where TValue : struct, IComparable, IConvertible, IComparable<TValue>, IEquatable<TValue>
        {
            private SerializedList<TValue> _value;
            
            public SerializedListProperty(SerializedProperty property)
                : base(property.name,
                    (ref SerializedObjectContainer c) => ((SerializedListProperty<TValue>)c.PropertyBag.FindProperty(property.name))._value,
                    (ref SerializedObjectContainer c, SerializedList<TValue> v) => ((SerializedListProperty<TValue>)c.PropertyBag.FindProperty(property.name))._value = v)
            {
                Property = property;
                _value = new SerializedList<TValue>(property);
            }

            public SerializedProperty Property { get; }
        }

        private interface ISerializedValueProvider
        {
            object GetObjectValue(SerializedProperty p);
            void SetObjectValue(SerializedProperty p, object v);
        }

        private interface ISerializedValueProvider<TValue> : ISerializedValueProvider
        {
            TValue GetValue(SerializedProperty p);
            void SetValue(SerializedProperty p, TValue v);
        }

        private class SerializedValueProvider<TValue> : ISerializedValueProvider<TValue>
        {
            public delegate TValue GetValueMethod(SerializedProperty p);
            public delegate void SetValueMethod(SerializedProperty p, TValue v);

            private GetValueMethod _getter;
            private SetValueMethod _setter;

            public SerializedValueProvider(GetValueMethod g, SetValueMethod s)
            {
                _getter = g;
                _setter = s;
            }

            public object GetObjectValue(SerializedProperty p)
            {
                return GetValue(p);
            }

            public void SetObjectValue(SerializedProperty p, object v)
            {
                SetValue(p, (TValue)v);
            }

            public TValue GetValue(SerializedProperty p)
            {
                return _getter(p);
            }

            public void SetValue(SerializedProperty p, TValue v)
            {
                _setter(p, v);
            }
        }
        
        private static Dictionary<string, ISerializedValueProvider> sPrimitiveProviders = new Dictionary<string, ISerializedValueProvider>()
        {
            { "bool", new SerializedValueProvider<bool>((p) => p.boolValue, (p,v) => p.boolValue = v) },
            { "char", new SerializedValueProvider<char>((p) => (char)p.intValue, (p,v) => p.intValue = v) },
            { "double", new SerializedValueProvider<double>((p) => p.doubleValue, (p,v) => p.doubleValue = v) },
            { "float", new SerializedValueProvider<float>((p) => p.floatValue, (p,v) => p.floatValue = v) },
            { "byte", new SerializedValueProvider<byte>((p) => (byte)p.intValue, (p,v) => p.intValue = v) },
            { "sbyte", new SerializedValueProvider<sbyte>((p) => (sbyte)p.intValue, (p,v) => p.intValue = v) },
            { "ushort", new SerializedValueProvider<ushort>((p) => (ushort)p.intValue, (p,v) => p.intValue = v) },
            { "short", new SerializedValueProvider<short>((p) => (short)p.intValue, (p,v) => p.intValue = v) },
            { "uint", new SerializedValueProvider<uint>((p) => (uint)p.longValue, (p,v) => p.longValue = v) },
            { "int", new SerializedValueProvider<int>((p) => p.intValue, (p,v) => p.intValue = v) },
            { "ulong", new SerializedValueProvider<ulong>((p) => (ulong)p.longValue, (p,v) => p.longValue = (long)v) },
            { "long", new SerializedValueProvider<long>((p) => p.longValue, (p,v) => p.longValue = v) },
        };
        
        private static ISerializedValueProvider CreatePrimitiveElementProvider(SerializedProperty p)
        {
            if (!p.isArray && !p.isFixedBuffer)
                return null;
            
            ISerializedValueProvider provider;
            sPrimitiveProviders.TryGetValue(p.arrayElementType, out provider);
            return provider;
        }

        private static IBaseSerializedProperty CreatePrimitiveListProperty(SerializedProperty p)
        {
            switch (p.arrayElementType)
            {
                case "bool": return new SerializedListProperty<bool>(p);
                case "char": return new SerializedListProperty<char>(p);
                case "double": return new SerializedListProperty<double>(p);
                case "float": return new SerializedListProperty<float>(p);
                case "byte": return new SerializedListProperty<byte>(p);
                case "sbyte": return new SerializedListProperty<sbyte>(p);
                case "ushort": return new SerializedListProperty<ushort>(p);
                case "short": return new SerializedListProperty<short>(p);
                case "uint": return new SerializedListProperty<uint>(p);
                case "int": return new SerializedListProperty<int>(p);
                case "ulong": return new SerializedListProperty<ulong>(p);
                case "long": return new SerializedListProperty<long>(p);
                default: return null;
            }
        }
        
        private class SerializedList<TItem> : IList<TItem>
            where TItem : struct, IComparable, IConvertible, IComparable<TItem>, IEquatable<TItem>
        {
            private SerializedProperty _property;
            private ISerializedValueProvider<TItem> _provider;

            public SerializedList(SerializedProperty p)
            {
                Assert.IsTrue(p.isArray || p.isFixedBuffer);
                _property = p;
                _provider = CreatePrimitiveElementProvider(p) as ISerializedValueProvider<TItem>;
            }

            private void CheckProvider()
            {
                if (_provider == null)
                {
                    throw new Exception($"Type {typeof(TItem).FullName} is not a supported primitive type");
                }
            }

            private struct ListEnumerator : IEnumerator<TItem>
            {
                private SerializedList<TItem> _source;
                private int _index;

                public ListEnumerator(SerializedList<TItem> source)
                {
                    _source = source;
                    _index = -1;
                }
                
                public bool MoveNext()
                {
                    if (_index >= _source.Count - 1)
                        return false;
                    
                    ++_index;
                    return true;
                }

                public void Reset()
                {
                    _index = -1;
                }

                public TItem Current => _source[_index];

                object IEnumerator.Current => Current;

                public void Dispose()
                {
                }
            }
            
            public IEnumerator<TItem> GetEnumerator()
            {
                return new ListEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(TItem item)
            {
                CheckProvider();
                var index = Count;
                _property.InsertArrayElementAtIndex(index);
                _provider.SetValue(_property.GetArrayElementAtIndex(index), item);
            }

            public void Clear()
            {
                _property.ClearArray();
            }

            public bool Contains(TItem item)
            {
                var index = IndexOf(item);
                if (index >= 0)
                {
                    return true;
                }
                return false;
            }

            public void CopyTo(TItem[] array, int arrayIndex)
            {
                var count = Count;
                var index = 0;
                for (var i = arrayIndex; i < count; ++i, ++index)
                {
                    array[i] = this[index];
                }
            }

            public bool Remove(TItem item)
            {
                var index = IndexOf(item);
                if (index >= 0)
                {
                    RemoveAt(index);
                    return true;
                }
                return false;
            }

            public int Count => _property.isArray ? _property.arraySize : _property.fixedBufferSize;

            public bool IsReadOnly => _property.isFixedBuffer;
            
            public int IndexOf(TItem item)
            {
                var count = Count;
                for (var i = 0; i < count; ++i)
                {
                    if (this[i].CompareTo(item) == 0)
                    {
                        return i;
                    }
                }
                return -1;
            }

            public void Insert(int index, TItem item)
            {
                CheckProvider();
                _property.InsertArrayElementAtIndex(index);
                _provider.SetValue(_property.GetArrayElementAtIndex(index), item);
            }

            public void RemoveAt(int index)
            {
                _property.DeleteArrayElementAtIndex(index);
            }

            public TItem this[int index]
            {
                get
                { 
                    CheckProvider();
                    return _provider.GetValue(_property.GetArrayElementAtIndex(index)); 
                }
                set
                {
                    CheckProvider();
                    _provider.SetValue(_property.GetArrayElementAtIndex(index), value);
                }
            }
        }
    }
}