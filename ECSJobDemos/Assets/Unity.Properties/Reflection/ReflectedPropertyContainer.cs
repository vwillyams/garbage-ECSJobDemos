using System;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.Properties.Reflection
{
    //A default property container that runs over standard c# reflection
    public class ReflectedPropertyContainer : IPropertyContainer, IVersionStorage
    {
        public object instance;

        public ReflectedPropertyContainer(object obj)
        {
            instance = obj;
            _version = 0;

            _propertyBag = FindOrCreatePropertyBag(obj.GetType());
        }

        private int _version;
        private readonly IPropertyBag _propertyBag;

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

        public IPropertyBag PropertyBag => _propertyBag;

        public IVersionStorage VersionStorage => this;
        
        private class ObjectWrapperProperty<TValue> : ReflectedProperty<TValue>, ISubtreeContainer
            where TValue : class
        {
            public ObjectWrapperProperty(PropertyInfo property)
                : base(property)
            {
            }

            private ReflectedPropertyContainer subContainer;

            public IPropertyContainer GetSubtreeContainer(IPropertyContainer c, IProperty p)
            {
                if (p == this)
                {
                    var value = GetObjectValue(ref c);
                    if (subContainer == null || !ReferenceEquals(subContainer.instance, value))
                    {
                        subContainer = new ReflectedPropertyContainer(value);
                    }

                    return subContainer;
                }

                return null;
            }
        }

        private class ObjectWrapperField<TValue> : ReflectedField<TValue>, ISubtreeContainer
            where TValue : class
        {
            public ObjectWrapperField(FieldInfo property)
                : base(property)
            {
            }

            private ReflectedPropertyContainer subContainer;

            public IPropertyContainer GetSubtreeContainer(IPropertyContainer c, IProperty p)
            {
                if (p == this)
                {
                    var value = GetObjectValue(ref c);
                    if (subContainer == null || !ReferenceEquals(subContainer.instance, value))
                    {
                        subContainer = new ReflectedPropertyContainer(value);
                    }

                    return subContainer;
                }

                return null;
            }
        }
        private static Dictionary<Type, PropertyBag> typeTrees = new Dictionary<Type, PropertyBag>();
        static IPropertyBag FindOrCreatePropertyBag(Type objType)
        {
            PropertyBag result;
            if (typeTrees.TryGetValue(objType, out result))
            {
                return result;
            }


            result = new PropertyBag();

            foreach (var prop in objType.GetProperties())
            {
                result.AddProperty(CreateProperty(prop));
            }

            foreach (var field in objType.GetFields())
            {
                result.AddProperty(CreateField(field));
            }


            typeTrees.Add(objType, result);

            return result;
        }

        static ReflectedProperty<TValue> CreateReflectedProperty<TValue>(PropertyInfo t)
        {
            return new ReflectedProperty<TValue>(t);
        }

        static ObjectWrapperProperty<TValue> CreateObjectWrapperProperty<TValue>(PropertyInfo t) where TValue : class
        {
            return new ObjectWrapperProperty<TValue>(t);
        }

        static ReflectedField<TValue> CreateReflectedField<TValue>(FieldInfo t)
        {
            return new ReflectedField<TValue>(t);
        }

        static ObjectWrapperField<TValue> CreateObjectWrapperField<TValue>(FieldInfo t) where TValue : class
        {
            return new ObjectWrapperField<TValue>(t);
        }


        private static MethodInfo s_DefaultReflectedPropertyCreation;
        private static MethodInfo s_ObjectWrapperCreation;
        private static MethodInfo s_DefaultReflectedFieldCreation;
        private static MethodInfo s_ObjectFieldWrapperCreation;

        private static void ResolveCreationMethods()
        {
            if (s_DefaultReflectedPropertyCreation == null)
            {
                s_DefaultReflectedPropertyCreation =
                    typeof(ReflectedPropertyContainer).GetMethod(nameof(CreateReflectedProperty), BindingFlags.Static | BindingFlags.NonPublic);
                s_ObjectWrapperCreation = typeof(ReflectedPropertyContainer).GetMethod(nameof(CreateObjectWrapperProperty), BindingFlags.Static | BindingFlags.NonPublic);
                s_DefaultReflectedFieldCreation =
                    typeof(ReflectedPropertyContainer).GetMethod(nameof(CreateReflectedField), BindingFlags.Static | BindingFlags.NonPublic);
                s_ObjectFieldWrapperCreation = typeof(ReflectedPropertyContainer).GetMethod(nameof(CreateObjectWrapperField), BindingFlags.Static | BindingFlags.NonPublic);
            }

        }

        private static IProperty CreateProperty(PropertyInfo field)
        {

            ResolveCreationMethods();
            Type t = field.PropertyType;
            if (t.IsClass)
            {
                MethodInfo classGeneric = s_ObjectWrapperCreation.MakeGenericMethod(t);
                return classGeneric.Invoke(null, new object[] { field }) as IProperty;

            }

            //Let's use reflection here as well because I'm too lazy to list all types by hand
            MethodInfo generic = s_DefaultReflectedPropertyCreation.MakeGenericMethod(t);
            return generic.Invoke(null, new object[] { field }) as IProperty;
        }

        private static IProperty CreateField(FieldInfo field)
        {
            ResolveCreationMethods();
            Type t = field.FieldType;
            if (t.IsClass)
            {
                MethodInfo classGeneric = s_ObjectFieldWrapperCreation.MakeGenericMethod(t);
                return classGeneric.Invoke(null, new object[] { field }) as IProperty;

            }

            //Let's use reflection here as well because I'm too lazy to list all types by hand
            MethodInfo generic = s_DefaultReflectedFieldCreation.MakeGenericMethod(t);
            return generic.Invoke(null, new object[] { field }) as IProperty;
        }

    }
}
