using System;
using System.Collections.Generic;
using Unity.Collections;

namespace AssemblyCSharp
{
	public static class NativeSortExtension
	{
		//@TODO: Lala... just quick fix... need native sort...
		static void Sort<T> (this NativeArray<T> array) where T : struct, IComparable<T>
		{
			List<T> tempArray = new List<T>(array.ToArray ());
			tempArray.Sort ();
			array.CopyFrom(tempArray.ToArray());
		}
	}
}

