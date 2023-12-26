using ArchiSteamFarm.Steam;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.TF2.Internal;

namespace SmallTail.Asf.TfBackpack;

public class Tf2BotHandler
{
    private const uint AppId = 440;
    
    private readonly Bot _bot;
    private readonly SteamGameCoordinator _steamGameCoordinator;

    private TaskCompletionSource? _connectRequest;
    private TaskCompletionSource<bool>? _isPremiumRequest;
    private TaskCompletionSource<uint>? _slotCountRequest;

    public Tf2BotHandler(Bot bot)
    {
        _bot = bot;
        
        _steamGameCoordinator = _bot.GetHandler<SteamGameCoordinator>()!;
    }

    private async Task Connect()
    {
        _connectRequest = new TaskCompletionSource();
        
        await _bot.Actions.Play([AppId]);
        
        var clientHello = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        _steamGameCoordinator.Send(clientHello, AppId);

        await _connectRequest.Task;

        _connectRequest = null;
    }

    private async Task Disconnect()
    {
        await _bot.Actions.Play([]);

        _bot.Actions.Resume();
    }

    public void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        Action<SteamGameCoordinator.MessageCallback>? handler = callback.EMsg switch
        {
            (uint)EGCBaseClientMsg.k_EMsgGCClientWelcome => HandleWelcome,
            (uint)ESOMsg.k_ESOMsg_CacheSubscriptionCheck => HandleCacheSubscriptionCheck,
            (uint)ESOMsg.k_ESOMsg_CacheSubscribed => HandleCacheSubscribed,
            _ => null
        };

        handler?.Invoke(callback);
    }

    private void HandleWelcome(SteamGameCoordinator.MessageCallback callback)
    {
        _connectRequest?.SetResult();
    }

    private void HandleCacheSubscriptionCheck(SteamGameCoordinator.MessageCallback callback)
    {
        var response = new ClientGCMsgProtobuf<CMsgSOCacheSubscriptionCheck>(callback.Message);
        
        var refreshRequest = new ClientGCMsgProtobuf<CMsgSOCacheSubscriptionRefresh>((uint)ESOMsg.k_ESOMsg_CacheSubscriptionRefresh);
        refreshRequest.Body.owner = response.Body.owner;
            
        _steamGameCoordinator.Send(refreshRequest, AppId);
    }

    private void HandleCacheSubscribed(SteamGameCoordinator.MessageCallback callback)
    {
        var response = new ClientGCMsgProtobuf<CMsgSOCacheSubscribed>(callback.Message);
        
        foreach (var subscribedType in response.Body.objects)
        {
            if (subscribedType.type_id == 7)
            {
                using var memoryStream = new MemoryStream(subscribedType.object_data[0]);
                
                var client = ProtoBuf.Serializer.Deserialize<CSOEconGameAccountClient>(memoryStream);

                _isPremiumRequest?.SetResult(!client.trial_account);
                _slotCountRequest?.SetResult((client.trial_account ? 50u : 300u) + client.additional_backpack_slots);
            }
        }
    }

    public async Task<uint> GetSlotCount()
    {
        _slotCountRequest = new TaskCompletionSource<uint>();
        
        await Connect();

        var slotCount = await _slotCountRequest.Task;

        _slotCountRequest = null;

        await Disconnect();
        
        return slotCount;
    }

    public async Task<bool> GetIsPremium()
    {
        _isPremiumRequest = new TaskCompletionSource<bool>();
        
        await Connect();
        
        var isPremium = await _isPremiumRequest.Task;

        _isPremiumRequest = null;

        await Disconnect();
        
        return isPremium;
    }

    public async Task UseItem(ulong itemId)
    {
        await Connect();
        
        var useItemRequest = new ClientGCMsgProtobuf<CMsgUseItem>((uint)EGCItemMsg.k_EMsgGCUseItemRequest);
        useItemRequest.Body.item_id = itemId;
        
        _steamGameCoordinator.Send(useItemRequest, AppId);

        await Disconnect();
    }
}