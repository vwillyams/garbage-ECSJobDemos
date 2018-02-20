using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Tests
{
    public class FastEqualityTests
    {
        struct Simple
        {
            int a;
            int b;
        }

        struct SimpleEmbedded
        {
            float4 a;
            int b;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct AlignSplit
        {
            float3 a;
            double b;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct EndPadding
        {
            double a;
            float b;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        unsafe struct FloatPointer
        {
            float* a;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct ClassInStruct
        {
            string blah;
        }
        
        
        [Test]
        public void SimpleLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(Simple));
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(8, res[0].count);
        }

        [Test]
        [Ignore("Fails")]
        public void PtrLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(FloatPointer));
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(8, res[0].count);
        }
        
        [Test]
        [Ignore("Fails")]
        public void ClassLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(ClassInStruct));
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(8, res[0].count);
        }
        
        [Test]
        public void SimpleEmbeddedLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(SimpleEmbedded));
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(20, res[0].count);
        }
        
        [Test]
        public void EndPaddingLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(EndPadding));
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(12, res[0].count);
        }
        
        [Test]
        public void AlignSplitLayout()
        {
            var res = AlignmentLayout.CreateLayout(typeof(AlignSplit));
            Assert.AreEqual(2, res.Length);
            
            Assert.AreEqual(0, res[0].offset);
            Assert.AreEqual(12, res[0].count);
            
            Assert.AreEqual(16, res[1].offset);
            Assert.AreEqual(8, res[1].count);
        }
    }
}
