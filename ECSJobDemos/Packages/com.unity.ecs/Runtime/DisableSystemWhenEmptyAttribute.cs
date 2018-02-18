using System;

namespace Unity.ECS
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DisableSystemWhenEmptyAttribute : System.Attribute { }
}