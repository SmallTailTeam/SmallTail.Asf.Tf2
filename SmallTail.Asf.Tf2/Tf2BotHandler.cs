using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.TF2.Internal;

namespace SmallTail.Asf.TfBackpack;

public class Tf2BotHandler
{
    private const uint AppId = 440;
    
    public bool IsPremium;
    public uint SlotCount;
    public List<CSOEconItem> Items = new();
    
    private readonly Bot _bot;
    private readonly SteamGameCoordinator _steamGameCoordinator;

    private TaskCompletionSource? _accountLoaded;
    private TaskCompletionSource? _itemsLoaded;

    public Tf2BotHandler(Bot bot)
    {
        _bot = bot;
        
        _steamGameCoordinator = _bot.GetHandler<SteamGameCoordinator>()!;
    }

    public async Task<(TaskCompletionSource AccountLoaded, TaskCompletionSource ItemsLoaded)> Connect()
    {
        _accountLoaded = new TaskCompletionSource();
        _itemsLoaded = new TaskCompletionSource();
        
        await _bot.Actions.Play([AppId]);
        
        var clientHello = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        _steamGameCoordinator.Send(clientHello, AppId);

        return (_accountLoaded, _itemsLoaded);
    }

    public async Task Disconnect()
    {
        await _bot.Actions.Play([]);

        _bot.Actions.Resume();
    }

    public void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        ASF.ArchiLogger.LogGenericInfo($"OnGCMessage : {callback.EMsg}");
        
        Action<SteamGameCoordinator.MessageCallback>? handler = callback.EMsg switch
        {
            (uint)ESOMsg.k_ESOMsg_CacheSubscriptionCheck => HandleCacheSubscriptionCheck,
            (uint)ESOMsg.k_ESOMsg_CacheSubscribed => HandleCacheSubscribed,
            _ => null
        };

        handler?.Invoke(callback);
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
            if (subscribedType.type_id == 1)
            {
                var items = subscribedType.object_data.Select(obj =>
                {
                    using var memoryStream = new MemoryStream(obj);

                    var item = ProtoBuf.Serializer.Deserialize<CSOEconItem>(memoryStream);

                    return item;
                }).ToList();

                Items = items;
                
                _itemsLoaded?.SetResult();
            }
            
            if (subscribedType.type_id == 7)
            {
                using var memoryStream = new MemoryStream(subscribedType.object_data[0]);
                
                var client = ProtoBuf.Serializer.Deserialize<CSOEconGameAccountClient>(memoryStream);

                IsPremium = !client.trial_account;
                SlotCount = (client.trial_account ? 50u : 300u) + client.additional_backpack_slots;
                
                _accountLoaded?.SetResult();
            }
        }
    }
    
    public void UseItem(ulong itemId)
    {
        var request = new ClientGCMsgProtobuf<CMsgUseItem>((uint)EGCItemMsg.k_EMsgGCUseItemRequest);
        request.Body.item_id = itemId;
        
        _steamGameCoordinator.Send(request, AppId);
    }
}