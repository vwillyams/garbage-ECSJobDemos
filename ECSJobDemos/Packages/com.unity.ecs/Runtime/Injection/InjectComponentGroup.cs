using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using Component = UnityEngine.Component;
using Transform = UnityEngine.Transform;
using TransformAccessArray = UnityEngine.Jobs.TransformAccessArray;

namespace Unity.Entities
{
    struct ProxyComponentData : IComponentData { }

    struct ProxySharedComponentData : ISharedComponentData { }

    class InjectComponentGroupData
    {
        ComponentGroup 		m_EntityGroup;
        readonly int 		m_GroupFieldOffset;

        readonly int 				m_EntityArrayOffset;
        readonly int                m_IndexFromEntityOffset;
        readonly int 				m_TransformAccessArrayOffset;
        readonly int 				m_LengthOffset;

        readonly InjectionData[]     m_ComponentDataInjections;
        readonly InjectionData[]     m_ComponentInjections;
        readonly InjectionData[]     m_FixedArrayInjections;
        readonly InjectionData[]     m_SharedComponentInjections;

        readonly InjectionContext       m_InjectionContext;

        InjectComponentGroupData(ComponentSystemBase system, FieldInfo groupField,
            InjectionData[] componentDataInjections, InjectionData[] componentInjections, InjectionData[] fixedArrayInjections, InjectionData[] sharedComponentInjections,
            FieldInfo entityArrayInjection, FieldInfo indexFromEntityInjection, FieldInfo transformAccessArrayInjection, InjectionContext injectionContext,
            FieldInfo lengthInjection, ComponentType[] componentRequirements)
        {
            var requiredComponentTypes = componentRequirements;

            m_EntityGroup = system.GetComponentGroup(requiredComponentTypes);

            for (var i = 0; i != componentInjections.Length; i++)
                componentInjections[i].IndexInComponentGroup = m_EntityGroup.GetIndexInComponentGroup(requiredComponentTypes[i].TypeIndex);

            m_ComponentDataInjections = componentDataInjections;
            m_ComponentInjections = componentInjections;
            m_FixedArrayInjections = fixedArrayInjections;
            m_SharedComponentInjections = sharedComponentInjections;
            m_InjectionContext = injectionContext;

            PatchGetIndexInComponentGroup(m_ComponentDataInjections);
            PatchGetIndexInComponentGroup(m_ComponentInjections);
            PatchGetIndexInComponentGroup(m_FixedArrayInjections);
            PatchGetIndexInComponentGroup(m_SharedComponentInjections);

            if (entityArrayInjection != null)
                m_EntityArrayOffset = UnsafeUtility.GetFieldOffset(entityArrayInjection);
            else
                m_EntityArrayOffset = -1;

            if (indexFromEntityInjection != null)
                m_IndexFromEntityOffset = UnsafeUtility.GetFieldOffset(indexFromEntityInjection);
            else
                m_IndexFromEntityOffset = -1;

            if (lengthInjection != null)
                m_LengthOffset = UnsafeUtility.GetFieldOffset(lengthInjection);
            else
                m_LengthOffset = -1;

            if (transformAccessArrayInjection != null)
                m_TransformAccessArrayOffset = UnsafeUtility.GetFieldOffset(transformAccessArrayInjection);
            else
                m_TransformAccessArrayOffset = -1;

            m_GroupFieldOffset = UnsafeUtility.GetFieldOffset(groupField);
        }

        public ComponentGroup EntityGroup => m_EntityGroup;

        void PatchGetIndexInComponentGroup(InjectionData[] componentInjections)
        {
            for (var i = 0; i != componentInjections.Length; i++)
                componentInjections[i].IndexInComponentGroup = m_EntityGroup.GetIndexInComponentGroup(componentInjections[i].ComponentType.TypeIndex);
        }

        public unsafe void UpdateInjection(byte* systemPtr)
        {
            var groupStructPtr = systemPtr + m_GroupFieldOffset;

            int length;
            ComponentChunkIterator iterator;
            m_EntityGroup.GetComponentChunkIterator(out length, out iterator);

            for (var i = 0; i != m_ComponentDataInjections.Length; i++)
            {
                ComponentDataArray<ProxyComponentData> data;
                m_EntityGroup.GetComponentDataArray(ref iterator, m_ComponentDataInjections[i].IndexInComponentGroup, length, out data);
                UnsafeUtility.CopyStructureToPtr(ref data, groupStructPtr + m_ComponentDataInjections[i].FieldOffset);
            }

            for (var i = 0; i != m_ComponentInjections.Length; i++)
            {
                ComponentArray<Component> data;
                m_EntityGroup.GetComponentArray(ref iterator, m_ComponentInjections[i].IndexInComponentGroup, length, out data);
                UnsafeUtility.CopyStructureToPtr(ref data, groupStructPtr + m_ComponentInjections[i].FieldOffset);
            }

            for (var i = 0; i != m_SharedComponentInjections.Length; i++)
            {
                SharedComponentDataArray<ProxySharedComponentData> data;
                m_EntityGroup.GetSharedComponentDataArray(ref iterator, m_SharedComponentInjections[i].IndexInComponentGroup, length, out data);
                UnsafeUtility.CopyStructureToPtr(ref data, groupStructPtr + m_SharedComponentInjections[i].FieldOffset);
            }

            for (var i = 0; i != m_FixedArrayInjections.Length; i++)
            {
                FixedArrayArray<int> data;
                m_EntityGroup.GetFixedArrayArray(ref iterator, m_FixedArrayInjections[i].IndexInComponentGroup, length, out data);
                UnsafeUtility.CopyStructureToPtr(ref data, groupStructPtr + m_FixedArrayInjections[i].FieldOffset);
            }

            if (m_TransformAccessArrayOffset != -1)
            {
                var transformsArray = m_EntityGroup.GetTransformAccessArray();
                UnsafeUtility.CopyStructureToPtr(ref transformsArray, groupStructPtr + m_TransformAccessArrayOffset);
            }

            if (m_EntityArrayOffset != -1)
            {
                EntityArray entityArray;
                m_EntityGroup.GetEntityArray(ref iterator, length, out entityArray);
                UnsafeUtility.CopyStructureToPtr(ref entityArray, groupStructPtr + m_EntityArrayOffset);
            }

            if (m_IndexFromEntityOffset != -1)
            {
                IndexFromEntity indexFromEntity;
                m_EntityGroup.GetIndexFromEntity(out indexFromEntity);
                UnsafeUtility.CopyStructureToPtr(ref indexFromEntity, groupStructPtr + m_IndexFromEntityOffset);
            }

            if (m_InjectionContext.HasEntries)
            {
                m_InjectionContext.UpdateInjection(m_EntityGroup, groupStructPtr);
            }

            if (m_LengthOffset != -1)
            {
                UnsafeUtility.CopyStructureToPtr(ref length, groupStructPtr + m_LengthOffset);
            }
        }

        public static InjectComponentGroupData CreateInjection(Type injectedGroupType, FieldInfo groupField, ComponentSystemBase system)
        {
            FieldInfo entityArrayField;
            FieldInfo indexFromEntityField;
            FieldInfo transformAccessArrayField;
            FieldInfo lengthField;

            var injectionContext = new InjectionContext();
            var componentDataInjections = new List<InjectionData>();
            var componentInjections = new List<InjectionData>();
            var fixedArrayInjections = new List<InjectionData>();
            var sharedComponentInjections = new List<InjectionData>();

            var componentRequirements = new HashSet<ComponentType>();
            var error = CollectInjectedGroup(injectedGroupType, out entityArrayField, out indexFromEntityField, out transformAccessArrayField, injectionContext, out lengthField, componentRequirements, componentDataInjections, componentInjections, fixedArrayInjections, sharedComponentInjections );
            if (error != null)
                throw new ArgumentException(error);

            return new InjectComponentGroupData(system, groupField, componentDataInjections.ToArray(), componentInjections.ToArray(), fixedArrayInjections.ToArray(), sharedComponentInjections.ToArray(), entityArrayField, indexFromEntityField,  transformAccessArrayField, injectionContext, lengthField, componentRequirements.ToArray());
        }

        static string CollectInjectedGroup(Type injectedGroupType, out FieldInfo entityArrayField, out FieldInfo indexFromEntityField, out FieldInfo transformAccessArrayField, InjectionContext injectionContext, out FieldInfo lengthField, ISet<ComponentType> componentRequirements,
            ICollection<InjectionData> componentDataInjections, ICollection<InjectionData> componentInjections, ICollection<InjectionData> fixedArrayInjections, ICollection<InjectionData> sharedComponentInjections)
        {
            //@TODO: Improve error messages...
            var fields = injectedGroupType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            transformAccessArrayField = null;
            entityArrayField = null;
            indexFromEntityField = null;
            lengthField = null;

            foreach(var field in fields)
            {
                var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;
                //@TODO: Prevent using GameObjectEntity, it will never show up. Point to GameObjectArray instead...

                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentDataArray<>))
                {
                    var injection = new InjectionData(field, field.FieldType.GetGenericArguments()[0], isReadOnly);
                    componentDataInjections.Add (injection);
                    componentRequirements.Add(injection.ComponentType);
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(SubtractiveComponent<>))
                {
                    componentRequirements.Add (ComponentType.Subtractive(field.FieldType.GetGenericArguments()[0]));
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(FixedArrayArray<>))
                {
                    var injection = new InjectionData(field, field.FieldType.GetGenericArguments()[0], isReadOnly);

                    fixedArrayInjections.Add (injection);
                    componentRequirements.Add(injection.ComponentType);
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentArray<>))
                {
                    if (isReadOnly)
                        return "[ReadOnly] may not be used on ComponentArray<>, it can only be used on ComponentDataArray<>";

                    var type = field.FieldType.GetGenericArguments()[0];
                    var injection = new InjectionData(field, type , false);

                    componentInjections.Add (injection);
                    componentRequirements.Add(injection.ComponentType);

                    if (type == typeof(Transform))
                        componentRequirements.Add(typeof(Transform));
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(SharedComponentDataArray<>))
                {
                    if (!isReadOnly)
                        return "SharedComponentDataArray<> must always be injected as [ReadOnly]";
                    var injection = new InjectionData(field, field.FieldType.GetGenericArguments()[0], true);

                    sharedComponentInjections.Add (injection);
                    componentRequirements.Add(injection.ComponentType);
                }
                else if (field.FieldType == typeof(TransformAccessArray))
                {
                    if (isReadOnly)
                        return "[ReadOnly] may not be used on a TransformAccessArray only on ComponentDataArray<>";
                    // Error on multiple transformAccessArray
                    if (transformAccessArrayField != null)
                        return "A [Inject] struct, may only contain a single TransformAccessArray";

                    transformAccessArrayField = field;
                    componentRequirements.Add(typeof(Transform));
                }
                else if (field.FieldType == typeof(EntityArray))
                {
                    // Error on multiple EntityArray
                    if (entityArrayField != null)
                        return "A [Inject] struct, may only contain a single EntityArray";

                    entityArrayField = field;
                }
                else if (field.FieldType == typeof(IndexFromEntity))
                {
                    // Error on multiple IndexFromEntity
                    if (indexFromEntityField != null)
                        return "A [Inject] struct, may only contain a single IndexFromEntity";

                    indexFromEntityField = field;
                }
                else if (field.FieldType == typeof(int))
                {
                    // Error on multiple EntityArray
                    if (field.Name != "Length")
                        return "A [Inject] struct, supports only a specialized int storing the length of the group. (\"int Length;\")";
                    lengthField = field;
                }
                else
                {
                    var hook = InjectionHookSupport.HookFor(field.FieldType);
                    if (hook == null)
                    {
                        return
                            $"[Inject] may only be used on ComponentDataArray<>, ComponentArray<>, TransformAccessArray, EntityArray, {string.Join(",", InjectionHookSupport.Hooks.Select(h => h.FieldTypeOfInterest.Name))} and int Length.";
                    }

                    var error = hook.ValidateField(field, isReadOnly, injectionContext);
                    if (error != null)
                    {
                        return error;
                    }

                    injectionContext.AddEntry(hook.CreateInjectionInfoFor(field, isReadOnly));
                }
            }

            if (injectionContext.HasComponentRequirements)
            {
                foreach (var requirement in injectionContext.ComponentRequirements)
                {
                    componentRequirements.Add(requirement);
                }
            }

            return null;
        }

    }
}
