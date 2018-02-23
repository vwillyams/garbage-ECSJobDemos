using Unity;

namespace Unity.Multiplayer
{
    public interface INetworkedMessage
    {
        void Serialize(ref ByteWriter writer);
        void Deserialize(ref ByteReader reader);
    }

}
