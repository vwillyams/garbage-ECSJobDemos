using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Properties.Serialization;

namespace Unity.Properties.ECS
{
    public interface IOptimizedVisitor : IBuiltInPropertyVisitor
        , IPropertyVisitor<float2>
        , IPropertyVisitor<float3>
        , IPropertyVisitor<float4>
        , IPropertyVisitor<float2x2>
        , IPropertyVisitor<float3x3>
        , IPropertyVisitor<float4x4>
    {
    }

    public static class OptimizedVisitor
    {
        public static bool Supports(Type t)
        {
            return s_OptimizedSet.Contains(t);
        }
        
        private static HashSet<Type> s_OptimizedSet;

        static OptimizedVisitor()
        {
            s_OptimizedSet = new HashSet<Type>();
            foreach (var it in typeof(IOptimizedVisitor).GetInterfaces())
            {
                if (typeof(IPropertyVisitor).IsAssignableFrom(it))
                {
                    var genArgs = it.GetGenericArguments();
                    if (genArgs.Length == 1)
                    {
                        s_OptimizedSet.Add(genArgs[0]);
                    }
                }
            }
        }
    }
    
    public class JsonVisitor : JsonPropertyVisitor, IOptimizedVisitor
    {
        public void Visit<TContainer>(ref TContainer container, VisitContext<float2> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.x},{context.Value.y}]");
            StringBuffer.Append(",\n");
        }

        public void Visit<TContainer>(ref TContainer container, VisitContext<float3> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.x},{context.Value.y},{context.Value.z}]");
            StringBuffer.Append(",\n");
        }

        public void Visit<TContainer>(ref TContainer container, VisitContext<float4> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.x},{context.Value.y},{context.Value.z},{context.Value.w}]");
            StringBuffer.Append(",\n");
        }

        public void Visit<TContainer>(ref TContainer container, VisitContext<float2x2> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.m0.x},{context.Value.m0.y},{context.Value.m1.x},{context.Value.m1.y}]");
            StringBuffer.Append(",\n");
        }

        public void Visit<TContainer>(ref TContainer container, VisitContext<float3x3> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.m0.x},{context.Value.m0.y},{context.Value.m0.z},{context.Value.m1.x},{context.Value.m1.y},{context.Value.m1.z},{context.Value.m2.x},{context.Value.m2.y},{context.Value.m2.z}]");
            StringBuffer.Append(",\n");
        }

        public void Visit<TContainer>(ref TContainer container, VisitContext<float4x4> context) where TContainer : IPropertyContainer
        {
            StringBuffer.Append(' ', Style.Space * Indent);
            StringBuffer.Append("\"");
            StringBuffer.Append(context.Property.Name);
            StringBuffer.Append("\": ");
            StringBuffer.Append($"[{context.Value.m0.x},{context.Value.m0.y},{context.Value.m0.z},{context.Value.m0.w},{context.Value.m1.x},{context.Value.m1.y},{context.Value.m1.z},{context.Value.m1.w},{context.Value.m2.x},{context.Value.m2.y},{context.Value.m2.z},{context.Value.m2.w},{context.Value.m3.x},{context.Value.m3.y},{context.Value.m3.z},{context.Value.m3.w}]");
            StringBuffer.Append(",\n");
        }
    }
}
