using UnityEditor.Experimental.UIElements.GraphView;

namespace Editor.UsingDataModel.NoPresenters
{
    internal static class NodeAdapters
    {
        internal static bool Adapt(this NodeAdapter value, PortSource<float> a, PortSource<float> b)
        {
            // run adapt code for float to float connections
            return true;
        }
    }
}
