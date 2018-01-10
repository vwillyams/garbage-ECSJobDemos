using Unity.Collections;
using Unity.Mathematics;
using Unity.Multiplayer;

// HACK (michalb): remake this whole thing!
public struct DespawnCommand : INetworkedMessage
{
    public int id;
    public DespawnCommand(int id)
    {
        this.id = id;
    }

    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(id);
    }

    public void Deserialize(ref ByteReader reader)
    {
        this.id = reader.ReadInt();
    }
}

public struct SpawnCommand : INetworkedMessage
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
    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(id);
        writer.Write(type);
        writer.Write(new float2(position.x, position.y));
        writer.Write(rotation.angle);
    }

    public void Deserialize(ref ByteReader reader)
    {
        this.id = reader.ReadInt();
        this.type = reader.ReadInt();
        var pos = reader.ReadFloat2();
        this.position = new PositionComponentData(pos.x, pos.y);
        this.rotation = new RotationComponentData(reader.ReadFloat());
    }
}

public struct MovementData : INetworkedMessage
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

    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(id);
        writer.Write(new float2(position.x, position.y));
        writer.Write(rotation.angle);
    }

    public void Deserialize(ref ByteReader reader)
    {
        this.id = reader.ReadInt();
        var pos = reader.ReadFloat2();
        this.position = new PositionComponentData(pos.x, pos.y);
        this.rotation = new RotationComponentData(reader.ReadFloat());
    }
}
