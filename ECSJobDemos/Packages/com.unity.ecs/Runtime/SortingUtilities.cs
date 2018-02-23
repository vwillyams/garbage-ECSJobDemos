namespace Unity.Entities
{
    struct SortingUtilities
	{
        public static unsafe void InsertSorted(ComponentType* data, int length, ComponentType newValue)
        {
            while (length > 0 && newValue < data[length-1])
            {
                data[length] = data[length-1];
                --length;
            }
            data[length] = newValue;
        }

		public static unsafe void InsertSorted(ComponentTypeInArchetype* data, int length, ComponentType newValue)
		{
			var newVal= new ComponentTypeInArchetype(newValue);
			while (length > 0 && newVal < data[length-1])
			{
				data[length] = data[length-1];
				--length;
			}
			data[length] = newVal;
		}

	}
}
