using QuickGraph;

namespace UnityEditor.ECS
{
//[DebuggerDisplay("{Source.ID} -> {Target.ID}")]
    public class EdgeAdapter : Edge<SystemViewData>
    {
        public string ID {get; private set;}

        public EdgeAdapter(string id, SystemViewData source, SystemViewData target)
            : base(source, target)
        {
            ID = id;
        }
    }

}
