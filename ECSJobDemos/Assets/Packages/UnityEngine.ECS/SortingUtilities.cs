namespace UnityEngine.ECS
{
	struct SortingUtilities
	{
        public static unsafe void InsertSorted(ComponentType* data, int length, ComponentType newValue)
        {
            while (length > 0 && data[length-1] > newValue)
            {
                data[length] = data[length-1];
                --length;
            }
            data[length] = newValue;
        }
		
	}
}