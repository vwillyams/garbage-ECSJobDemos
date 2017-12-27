using QuickGraph;
using UnityEditor.Experimental.UIElements.GraphView;

//[DebuggerDisplay("{Source.ID} -> {Target.ID}")]
public class EdgeAdapter : Edge<Node>
{
    public string ID {get; private set;}

    public EdgeAdapter(string id, Node source, Node target)
            : base(source, target)
    {
        ID = id;
    }
}
