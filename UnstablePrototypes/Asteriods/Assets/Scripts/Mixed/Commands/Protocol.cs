using Unity.Multiplayer;
using Unity.Collections;

public enum SpawnType
{
    Ship,
    Asteroid,
    Bullet
}

public struct Command : INetworkedMessage
{
    int sequence;
    NativeList<PlayerInputComponentData> InputCommands;

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
        foreach(var input in inputs)
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
        }
    }
}