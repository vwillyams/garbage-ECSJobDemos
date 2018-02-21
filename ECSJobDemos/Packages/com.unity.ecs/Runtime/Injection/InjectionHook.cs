using System;
using System.Reflection;
using System.Collections.Generic;

namespace Unity.ECS
{
    sealed class CustomInjectionHookAttribute : Attribute
    {

    }

    public sealed class InjectionContext
    {
        public struct Entry
        {
            public int FieldOffset;
            public FieldInfo FieldInfo;
            public Type[] ComponentRequirements;
            public IInjectionHook Hook;
        }

        readonly List<Entry> m_Entries = new List<Entry>();

        public bool HasComponentRequirements { get; private set; }

        public bool HasEntries => m_Entries.Count != 0;

        public IReadOnlyCollection<Entry> Entries => m_Entries;

        public IEnumerable<ComponentType> ComponentRequirements
        {
            get
            {
                foreach (var info in m_Entries)
                {
                    foreach (var requirement in info.ComponentRequirements)
                    {
                        yield return requirement;
                    }
                }
            }
        }

        internal void AddEntry(Entry entry)
        {
            HasComponentRequirements = HasComponentRequirements || entry.ComponentRequirements.Length > 0;
            m_Entries.Add(entry);
        }

        internal unsafe void UpdateInjection(ComponentGroup entityGroup, byte* groupStructPtr)
        {
            if(!HasEntries)
                return;

            foreach (var info in m_Entries)
            {
                info.Hook.UpdateInjection(info, entityGroup, groupStructPtr);
            }
        }
    }

    public interface IInjectionHook
    {
        Type FieldTypeOfInterest { get; }

        string ValidateField(FieldInfo field, bool isReadOnly, InjectionContext injectionInfo);

        InjectionContext.Entry CreateInjectionInfoFor(FieldInfo field, bool isReadOnly);

        unsafe void UpdateInjection(InjectionContext.Entry info, ComponentGroup entityGroup, byte* groupStructPtr);
    }

    public static class InjectionHookSupport
    {
        static bool s_HasHooks;
        static readonly List<IInjectionHook> k_Hooks = new List<IInjectionHook>();

        internal static IReadOnlyCollection<IInjectionHook> Hooks => k_Hooks;

        public static void RegisterHook(IInjectionHook hook)
        {
            var registeredHook = HookFor(hook.FieldTypeOfInterest);
            if (registeredHook != null)
            {
                Debug.LogError($"Custom injection hook for type {hook.FieldTypeOfInterest.FullName} has already been registered ({registeredHook.GetType().FullName}). {hook.GetType().FullName} will be skipped.");
            }
            else
            {
                s_HasHooks = true;
                k_Hooks.Add(hook);
            }
        }

        public static void UnregisterHook(IInjectionHook hook)
        {
            k_Hooks.Remove(hook);
            s_HasHooks = k_Hooks.Count != 0;
        }

        internal static IInjectionHook HookFor(Type type)
        {
            if (!s_HasHooks)
                return null;

            // I'm not expecting many hooks, I think it is fine to iterate over them
            foreach (var hook in k_Hooks)
            {
                if (hook.FieldTypeOfInterest == type)
                    return hook;
            }

            return null;
        }

        public static bool IsValidHook(Type type)
        {
            if (type.IsAbstract)
                return false;
            if (type.ContainsGenericParameters)
                return false;
            if (!typeof(IInjectionHook).IsAssignableFrom(type))
                return false;
            return type.GetCustomAttributes(typeof(CustomInjectionHookAttribute), true).Length != 0;
        }
    }
}
