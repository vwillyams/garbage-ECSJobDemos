using UnityEngine.ECS;

public struct PlayerTagComponentData : IComponentData
{
}

public struct ShipTagComponentData : IComponentData
{
}

public struct ShipStateComponentData : IComponentData
{
    public enum ShipState
    {
        Idle,
        Thrust
    }

    public ShipStateComponentData(int state)
    {
        State = state;
    }

    public ShipStateComponentData(ShipState state)
    {
        State = (int)state;
    }
    public int State;
}

public struct ShipInfoComponentData : IComponentData
{
    public Entity entity;
    //public int entity;
    public ShipInfoComponentData(Entity e)
    //public ShipInfoComponentData(int e)
    {
        entity = e;
    }
}
