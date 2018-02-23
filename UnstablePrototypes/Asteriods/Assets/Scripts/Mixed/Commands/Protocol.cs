using System;

using Unity.Multiplayer;
using Unity.Collections;

public enum AsteroidsProtocol
{
    Command,
    Snapshot,
    ReadyReq,
    ReadyRsp
}

public enum SpawnType
{
    Ship,
    Asteroid,
    Bullet
}

public struct ReadyRsp : INetworkedMessage
{
    public int NetworkId;
    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(NetworkId);
    }
    public void Deserialize(ref ByteReader reader)
    {
        NetworkId = reader.ReadInt();
    }
}

public struct Command : INetworkedMessage, IDisposable
{
    int sequence;
    public NativeList<PlayerInputComponentData> InputCommands;

    public Command(int sequence, Allocator allocator)
    {
        this.sequence = sequence;
        InputCommands = new NativeList<PlayerInputComponentData>(allocator);
    }

    public void Dispose()
    {
        if (InputCommands.IsCreated)
            InputCommands.Dispose();
    }

    public void Serialize(ref ByteWriter writer)
    {
        writer.Write(sequence);
        writer.Write(InputCommands.Length);

        NativeArray<PlayerInputComponentData> inputs = InputCommands;
        foreach (var input in inputs)
        {
            writer.Write(input.left);
            writer.Write(input.right);
            writer.Write(input.thrust);
            writer.Write(input.shoot);
        }
    }

    public void Deserialize(ref ByteReader reader)
    {
        sequence = reader.ReadInt();
        int length = reader.ReadInt();

        for (int i = 0; i < length; ++i)
        {
            var input = new PlayerInputComponentData();
            input.left = reader.ReadByte();
            input.right = reader.ReadByte();
            input.thrust = reader.ReadByte();
            input.shoot = reader.ReadByte();
            InputCommands.Add(input);
        }
    }
}

public struct Snapshot : INetworkedMessage, IDisposable
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
        foreach (var command in spawns)
        {
            command.Serialize(ref writer);
        }
        writer.Write(DespawnCommands.Length);
        NativeArray<DespawnCommand> despawns = DespawnCommands;
        foreach (var command in despawns)
        {
            command.Serialize(ref writer);
        }
        writer.Write(MovementDatas.Length);
        NativeArray<MovementData> movements = MovementDatas;
        foreach (var command in movements)
        {
            command.Serialize(ref writer);
        }
    }

    public void Deserialize(ref ByteReader reader)
    {
        sequence = reader.ReadInt();
        int length = 0;
        length = reader.ReadInt();
        for (int i = 0; i < length; ++i)
        {
            var command = new SpawnCommand();
            command.Deserialize(ref reader);
            SpawnCommands.Add(command);
        }
        length = reader.ReadInt();
        for (int i = 0; i < length; ++i)
        {
            var command = new DespawnCommand();
            command.Deserialize(ref reader);
            DespawnCommands.Add(command);
        }
        length = reader.ReadInt();
        for (int i = 0; i < length; ++i)
        {
            var command = new MovementData();
            command.Deserialize(ref reader);
            MovementDatas.Add(command);
        }
    }
}
