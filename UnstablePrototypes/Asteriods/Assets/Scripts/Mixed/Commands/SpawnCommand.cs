using Unity.Collections;
using Unity.Mathematics;
using Unity.Multiplayer;

// HACK (michalb): remake this whole thing!
public interface INetworkedMessage
{
    void Serialize(ref ByteWriter writer);
    void Deserialize(ref ByteReader reader);
}

public struct Snapshot :  INetworkedMessage
{
    int sequence;
    public NativeList<SpawnCommand> SpawnCommands;
    public NativeList<DespawnCommand> DespawnCommands;
    public NativeList<MovementData> MovementDatas;

    public Snapshot(int sequence, Allocator allocator)
    {
        this.sequence = sequence;
        SpawnCommands = new NativeList<SpawnCommand>(allocator);
        DespawnCommands = new NativeList<DespawnCommand>(allocator);
        MovementDatas = new NativeList<MovementData>(allocator);
    }

    public void Dispose()
    {
        if (SpawnCommands.IsCreated)
            SpawnCommands.Dispose();
        if (DespawnCommands.IsCreated)
            DespawnCommands.Dispose();
        if (MovementDatas.IsCreated)
            MovementDatas.Dispose();
    }

    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(sequence);
        writer.Write(SpawnCommands.Length);
        NativeArray<SpawnCommand> spawns = SpawnCommands;
        foreach(var command in spawns)
        {
            command.Serialize(ref writer);
        }
        writer.Write((short)DespawnCommands.Length);
        NativeArray<DespawnCommand> despawns = DespawnCommands;
        foreach(var command in despawns)
        {
            command.Serialize(ref writer);
        }
        writer.Write((short)MovementDatas.Length);
        NativeArray<MovementData> movements = MovementDatas;
        foreach(var command in movements)
        {
            command.Serialize(ref writer);
        }
    }

    public void Deserialize(ref ByteReader reader)
    {
        sequence = reader.ReadInt();
        int length = 0;
        length = reader.ReadShort();
        for (int i = 0; i < length; ++i)
        {
            var command = new SpawnCommand();
            command.Deserialize(ref reader);
            SpawnCommands.Add(command);
        }
        length = reader.ReadShort();
        for (int i = 0; i < length; ++i)
        {
            var command = new DespawnCommand();
            command.Deserialize(ref reader);
            DespawnCommands.Add(command);
        }
        length = reader.ReadShort();
        for (int i = 0; i < length; ++i)
        {
            var command = new MovementData();
            command.Deserialize(ref reader);
            MovementDatas.Add(command);
        }
    }
}

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