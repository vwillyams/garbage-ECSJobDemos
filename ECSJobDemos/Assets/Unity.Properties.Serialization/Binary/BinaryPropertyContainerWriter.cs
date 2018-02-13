using System.IO;

namespace Unity.Properties.Serialization
{
    public static class BinaryPropertyContainerWriter
    {
        private static readonly MemoryStream s_MemoryStream = new MemoryStream();
        private static readonly BinaryWriter s_BinaryWriter = new BinaryWriter(s_MemoryStream);
        
        public static void Write<TContainer>(Stream stream, TContainer container, BinaryPropertyVisitor visitor) 
            where TContainer : IPropertyContainer
        {
            s_MemoryStream.Position = 0;
            visitor.Writer = s_BinaryWriter;
            
            s_BinaryWriter.Write(BinaryToken.BeginObject);
            s_BinaryWriter.Write((ushort) 0);
            container.PropertyBag.Visit(ref container, visitor);
            s_BinaryWriter.Write(BinaryToken.EndObject);

            // Prepend the length
            const int start = 3;
            var end = s_MemoryStream.Position;
            var size = end - start;
            
            s_MemoryStream.Position = start - sizeof(ushort);
            s_BinaryWriter.Write((ushort) size);
            s_MemoryStream.Position = end;
            
            stream.Write(s_MemoryStream.GetBuffer(), 0, (int) s_MemoryStream.Position);
        }
    }
}