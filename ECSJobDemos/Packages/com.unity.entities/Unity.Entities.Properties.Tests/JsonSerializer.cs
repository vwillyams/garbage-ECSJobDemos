using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Unity.Properties;
using Unity.Properties.Serialization;

namespace Unity.Entities.Properties.Tests
{
    /// <summary>
    /// Helper class for high level use cases
    /// @NOTE This will be included in the Properties API package eventually.
    /// </summary>
    public static class JsonSerializer
    {
        public static string Serialize<TContainer>(TContainer container)
            where TContainer : struct, IPropertyContainer
        {
            var visitor = new JsonVisitor {StringBuffer = new StringBuffer(4096)};
            Visit(container, visitor);
            return visitor.StringBuffer.ToString();
        }

        public static void Visit<TContainer>(TContainer container, JsonVisitor visitor)
            where TContainer : struct, IPropertyContainer
        {
            WritePrefix(visitor);
            container.PropertyBag.VisitStruct(ref container, visitor);
            WriteSuffix(visitor);
        }

        /// <summary>
        /// Writes the BeginObject scope
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        private static void WritePrefix(JsonPropertyVisitor visitor)
        {
            var buffer = visitor.StringBuffer;
            buffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            buffer.Append("{\n");
            visitor.Indent++;
        }

        /// <summary>
        /// Writes the CloseObject scope
        /// </summary>
        private static void WriteSuffix(JsonPropertyVisitor visitor)
        {
            var buffer = visitor.StringBuffer;
            visitor.Indent--;

            buffer.Length -= 2;
            buffer.Append("\n");
            buffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            buffer.Append("}");
        }
    }
}