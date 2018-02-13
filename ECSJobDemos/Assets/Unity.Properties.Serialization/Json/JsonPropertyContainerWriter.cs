namespace Unity.Properties.Serialization
{
    public static class JsonPropertyContainerWriter
    {
        private static readonly StringBuffer s_StringBuffer = new StringBuffer(1024);
        private static readonly JsonPropertyVisitor s_DefaultVisitor = new JsonPropertyVisitor { StringBuffer = s_StringBuffer };
        
        public static string Write<TContainer>(TContainer container, JsonPropertyVisitor visitor = null) 
            where TContainer : IPropertyContainer
        {
            if (null == visitor)
            {
                visitor = s_DefaultVisitor;
            }

            visitor.StringBuffer = s_StringBuffer;
            
            s_StringBuffer.Clear();
            s_StringBuffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            s_StringBuffer.Append("{\n");
            
            visitor.Indent++;
            
            container.PropertyBag.Visit(ref container, visitor);
            
            visitor.Indent--;
            
            s_StringBuffer.Length -= 2;
            s_StringBuffer.Append("\n");
            s_StringBuffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            s_StringBuffer.Append("}");

            return s_StringBuffer.ToString();
        }
    }
}