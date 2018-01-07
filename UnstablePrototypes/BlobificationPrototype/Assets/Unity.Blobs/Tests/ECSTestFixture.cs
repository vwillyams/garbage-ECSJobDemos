using UnityEngine.ECS;
using NUnit.Framework;

//@TODO: This is just duplicated code... Can't reference test assembly from another test assembly...

namespace UnityEngine.ECS.Tests
{
	public class ECSTestsFixture
	{
		protected World 			m_PreviousWorld;
		protected World 			World;
		protected EntityManager     m_Manager;

        [SetUp]
		public void Setup()
		{
			m_PreviousWorld = World.Active;
			World = World.Active = new World ("Test World");

			m_Manager = World.GetOrCreateManager<EntityManager> ();
		}

		[TearDown]
		public void TearDown()
		{
            if (m_Manager != null)
            {
	            World.Dispose();
	            World = null;
	            
                World.Active = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = null;
            }
		}
	}
}