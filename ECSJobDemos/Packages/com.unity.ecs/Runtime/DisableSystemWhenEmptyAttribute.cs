using System;

namespace Unity.Entities
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DisableSystemWhenEmptyAttribute : System.Attribute { }
}