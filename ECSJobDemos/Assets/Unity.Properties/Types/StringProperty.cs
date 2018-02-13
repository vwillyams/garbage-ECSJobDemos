namespace Unity.Properties
{
	public class StringProperty<TContainer> : Property<TContainer, string> 
		where TContainer : IPropertyContainer
	{
		public StringProperty(string name, GetValueMethod getValue, SetValueMethod setValue) : base(name, getValue, setValue)
		{
		}
	}
}