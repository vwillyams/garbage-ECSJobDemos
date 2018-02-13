using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Properties.Serialization
{
    public static class BinaryToken
    {
        public const byte None = 0;
        public const byte BeginObject = 1;
        public const byte EndObject = 2;
        public const byte BeginArray = 3;
        public const byte EndArray = 4;
        public const byte Property = 5;
        public const byte Value = 6;
    }

    public class BinaryPropertyVisitor : IBuiltInPropertyVisitor
    {
        private readonly Stack<long> m_PositionStack = new Stack<long>();
        
        public BinaryWriter Writer { protected get; set; }
        
        protected BinaryPropertyVisitor()
        {
        }

        protected void WriteValuePropertyHeader(string name, TypeCode typeCode, bool array = false)
        {
            if (!array)
            {
                Writer.Write(BinaryToken.Property);
                Writer.Write(name);
            }
            Writer.Write(BinaryToken.Value);
            Writer.Write((byte) typeCode);
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<sbyte> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.SByte, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<short> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Int16, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<int> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Int32, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<long> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Int64, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<byte> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Byte, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<ushort> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.UInt16, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<uint> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.UInt32, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<ulong> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.UInt64, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<float> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Single, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<double> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Double, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<string> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.String, context.Index != -1);
            Writer.Write(context.Value ?? string.Empty);
            return true;
        }
        
        public bool Visit<TContainer>(ref TContainer container, VisitContext<bool> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Boolean, context.Index != -1);
            Writer.Write(context.Value);
            return true;
        }

        public bool Visit<TContainer>(ref TContainer container, VisitContext<char> context) where TContainer : IPropertyContainer
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.String, context.Index != -1);
            Writer.Write(context.Value.ToString());
            return true;
        }

        public bool Visit<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context) where TContainer : IPropertyContainer
        {
            throw new NotImplementedException();
        }

        public bool VisitEnum<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context)
            where TContainer : IPropertyContainer
            where TValue : struct
        {
            WriteValuePropertyHeader(context.Property?.Name, TypeCode.Int32, context.Index != -1);
            Writer.Write(Convert.ToInt32(context.Value));
            return true;
        }

        public bool BeginSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
        {
            if (context.Index == -1)
            {
                Writer.Write(BinaryToken.Property);
                Writer.Write(context.Property?.Name);
            }
            Writer.Write(BinaryToken.BeginObject);
            Writer.Write((ushort) 0);
            m_PositionStack.Push(Writer.BaseStream.Position);
            return true;
        }

        public void EndSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
        {
            Writer.Write(BinaryToken.EndObject);
            PrependSize();
        }

        public bool BeginList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
        {
            if (context.Index == -1)
            {
                Writer.Write(BinaryToken.Property);
                Writer.Write(context.Property?.Name);
            }
            Writer.Write(BinaryToken.BeginArray);
            Writer.Write((ushort) 0);
            m_PositionStack.Push(Writer.BaseStream.Position);
            return true;
        }

        public void EndList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
        {
            Writer.Write(BinaryToken.EndArray);
            PrependSize();
        }

        private void PrependSize()
        {
            var start = m_PositionStack.Pop();
            var end = Writer.BaseStream.Position;
            var size = end - start;

            Writer.BaseStream.Position = start - sizeof(ushort);
            Writer.Write((ushort) size);
            Writer.BaseStream.Position = end;    
        }
    }
}