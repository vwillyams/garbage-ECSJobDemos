public struct SpawnCommand
{
    public int id;
    public int type;
    public PositionComponentData position;
    public RotationComponentData rotation;

    public SpawnCommand(int id, int type, PositionComponentData position, RotationComponentData rotation)
    {
        this.id = id;
        this.type = type;
        this.position = position;
        this.rotation = rotation;
    }
}

public struct MovementData
{
    public int id;
    public PositionComponentData position;
    public RotationComponentData rotation;

    public MovementData(int id, PositionComponentData position, RotationComponentData rotation)
    {
        this.id = id;
        this.position = position;
        this.rotation = rotation;
    }
}