using SteamKit2.GC.TF2.Internal;
using SteamKit2.Internal;

namespace SmallTail.Asf.TfBackpack.Models;

public class DeleteMsg : IGCSerializableMessage
{
    public void Serialize(Stream stream)
    {
    }

    public void Deserialize(Stream stream)
    {
    }

    public uint GetEMsg() 
        => (uint)EGCItemMsg.k_EMsgGCDelete;
}