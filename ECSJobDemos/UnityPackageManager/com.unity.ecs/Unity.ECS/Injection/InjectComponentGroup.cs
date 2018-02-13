using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Jobs;

namespace Unity.ECS
{
    internal struct ProxyComponentData : IComponentData { }

    internal struct ProxySharedComponentData : ISharedComponentData { }

    internal class InjectComponentGroupData
    {
        private ComponentGroup 		m_EntityGroup;
        private ComponentType[]     m_ComponentRequirements;
        private readonly int 		m_GroupFieldOffset;

        private readonly int 				m_EntityArrayOffset;
        private readonly int 				m_TransformAccessArrayOffset;
        private readonly int 				m_LengthOffset;
        private readonly int                 m_GameObjectArrayOffset;

        private readonly InjectionData[]     m_ComponentDataInjections;
        private readonly InjectionData[]     m_ComponentInjections;
        private readonly InjectionData[]     m_FixedArrayInjections;
        private readonly InjectionData[]     m_SharedComponentInjections;

        private InjectComponentGroupData(EntityManager entityManager, FieldInfo groupField,
            InjectionData[] componentDataInjections, InjectionData[] componentInjections, InjectionData[] fixedArrayInjections, InjectionData[] sharedComponentInjections,
        {
            var requiredComponentTypes = componentRequirements;

            m_EntityGroup = entityManager.CreateComponentGroup(requiredComponentTypes);

            for (var i = 0; i != componentInjections.Length; i++)
                componentInjections[i].IndexInComponentGroup = m_EntityGroup.GetIndexInComponentGroup(requiredComponentTypes[i].TypeIndex);

            m_ComponentDataInjections = componentDataInjections;
            m_ComponentInjections = componentInjections;
            m_FixedArrayInjections = fixedArrayInjections;
            m_SharedComponentInjections = sharedComponentInjections;

            PatchGetIndexInComponentGroup(m_ComponentDataInjections);
            PatchGetIndexInComponentGroup(m_ComponentInjections);
            PatchGetIndexInComponentGroup(m_FixedArrayInjections);
            PatchGetIndexInComponentGroup(m_SharedComponentInjections);

            if (entityArrayInjection != null)
                m_EntityArrayOffset = UnsafeUtility.GetFieldOffset(entityArrayInjection);
            else
                m_EntityArrayOffset = -1;

            if (lengthInjection != null)
                m_LengthOffset = UnsafeUtility.GetFieldOffset(lengthInjection);
            else
                m_LengthOffset = -1;

            if (gameObjectArrayInjection != null)
                m_GameObjectArrayOffset = UnsafeUtility.GetFieldOffset(gameObjectArrayInjection);
            else
                m_GameObjectArrayOffset = -1;

            if (transformAccessArrayInjection != null)
                m_TransformAccessArrayOffset = UnsafeUtility.GetFieldOffset(transformAccessArrayInjection);
            else
                m_TransformAccessArrayOffset = -1;

            m_GroupFieldOffset = UnsafeUtility.GetFieldOffset(groupField);
        }

        public void Dispose()
        {
            m_EntityGroup.Dispose ();
            m_EntityGroup = null;
        }

        public ComponentGroup EntityGroup => m_EntityGroup;

        private void PatchGetIndexInComponentGroup(InjectionData[] componentInjections)
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

            if (m_GameObjectArrayOffset != -1)
            {
                var gameObjectArray = m_EntityGroup.GetGameObjectArray();
                UnsafeUtility.CopyStructureToPtr(ref gameObjectArray, groupStructPtr + m_GameObjectArrayOffset);
            }

            if (m_LengthOffset != -1)
            {
                UnsafeUtility.CopyStructureToPtr(ref length, groupStructPtr + m_LengthOffset);
            }
        }

        public static InjectComponentGroupData CreateInjection(Type injectedGroupType, FieldInfo groupField, EntityManager entityManager)
        {
            FieldInfo entityArrayField;
            FieldInfo gameObjectArrayField;
            FieldInfo transformAccessArrayField;
            FieldInfo lengthField;

            var componentDataInjections = new List<InjectionData>();
            var componentInjections = new List<InjectionData>();
            var fixedArrayInjections = new List<InjectionData>();
            var sharedComponentInjections = new List<InjectionData>();

            var componentRequirements = new List<ComponentType>();
            var error = CollectInjectedGroup(injectedGroupType, out entityArrayField, out transformAccessArrayField, out gameObjectArrayField, out lengthField, componentRequirements, componentDataInjections, componentInjections, fixedArrayInjections, sharedComponentInjections);
            if (error != null)
                throw new System.ArgumentException(error);

            return new InjectComponentGroupData(entityManager, groupField, componentDataInjections.ToArray(), componentInjections.ToArray(), fixedArrayInjections.ToArray(), sharedComponentInjections.ToArray(), entityArrayField, transformAccessArrayField, gameObjectArrayField, lengthField, componentRequirements.ToArray());
        }

        private static string CollectInjectedGroup(Type injectedGroupType, out FieldInfo entityArrayField, out FieldInfo transformAccessArrayField, out FieldInfo gameObjectArrayField, out FieldInfo lengthField, ICollection<ComponentType> componentRequirements,
            ICollection<InjectionData> componentDataInjections, ICollection<InjectionData> componentInjections, ICollection<InjectionData> fixedArrayInjections, ICollection<InjectionData> sharedComponentInjections)
        {
            //@TODO: Improve error messages...
            var fields = injectedGroupType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            transformAccessArrayField = null;
            entityArrayField = null;
            gameObjectArrayField = null;
            lengthField = null;
            var explicitTransformRequirement = false;
            var implicitTransformRequirement = false;

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
                        explicitTransformRequirement = true;
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
                    implicitTransformRequirement = true;
                }
                else if (field.FieldType == typeof(EntityArray))
                {
                    // Error on multiple EntityArray
                    if (entityArrayField != null)
                        return "A [Inject] struct, may only contain a single EntityArray";

                    entityArrayField = field;
                }
                else if (field.FieldType == typeof(GameObjectArray))
                {
                    if (isReadOnly)
                        return "[ReadOnly] may not be used on GameObjectArray, it can only be used on ComponentDataArray<>";
                    // Error on multiple GameObjectArray
                    if (gameObjectArrayField != null)
                        return "A [Inject] struct, may only contain a single GameObjectArray";

                    gameObjectArrayField = field;
                    implicitTransformRequirement = true;
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
                    return
                        "[Inject] may only be used on ComponentDataArray<>, ComponentArray<>, TransformAccessArray, EntityArray, GameObjectArray and int Length.";
                }
            }

            if (!explicitTransformRequirement && implicitTransformRequirement)
            {
                componentRequirements.Add(typeof(Transform));
            }

            return null;
        }

    }
}
