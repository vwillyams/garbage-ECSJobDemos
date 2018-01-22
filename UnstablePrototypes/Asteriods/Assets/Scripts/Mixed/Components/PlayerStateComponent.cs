using UnityEngine;
using UnityEngine.ECS;

public struct EntityTypeComponentData : IComponentData
{
    public int Type;
}

public struct PlayerStateComponentData : IComponentData
{
    public enum PlayerState
    {
        None,
        Connecting,
        Loading,
        Ready,
        Playing,
        Alive,
        Dead
    }

    public PlayerStateComponentData(int state)
    {
        State = state;
    }

    public PlayerStateComponentData(PlayerState state)
    {
        State = (int)state;
    }
    public int State;
}
