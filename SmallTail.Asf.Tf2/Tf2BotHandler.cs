using ArchiSteamFarm.Steam;
using SmallTail.Asf.TfBackpack.Models;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.TF2.Internal;

namespace SmallTail.Asf.TfBackpack;

public class Tf2BotHandler
{
    private const uint AppId = 440;
    private static readonly TimeSpan ItemsUseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ItemsDeleteDelay = TimeSpan.FromSeconds(1);
    
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
            switch (subscribedType.type_id)
            {
                case 1:
                {
                    Items.Clear();
                    
                    var items = subscribedType.object_data.Select(obj =>
                    {
                        using var memoryStream = new MemoryStream(obj);

                        var item = ProtoBuf.Serializer.Deserialize<CSOEconItem>(memoryStream);

                        return item;
                    });

                    Items.AddRange(items);
                
                    _itemsLoaded?.TrySetResult();
                    break;
                }
                case 7:
                {
                    using var memoryStream = new MemoryStream(subscribedType.object_data[0]);
                
                    var client = ProtoBuf.Serializer.Deserialize<CSOEconGameAccountClient>(memoryStream);

                    IsPremium = !client.trial_account;
                    SlotCount = (client.trial_account ? 50u : 300u) + client.additional_backpack_slots;
                
                    _accountLoaded?.TrySetResult();
                    break;
                }
            }
        }
    }
    
    public void UseItem(ulong itemId)
    {
        var request = new ClientGCMsgProtobuf<CMsgUseItem>((uint)EGCItemMsg.k_EMsgGCUseItemRequest);
        request.Body.item_id = itemId;
        
        _steamGameCoordinator.Send(request, AppId);
    }
    
    public async Task UseItems(IReadOnlyCollection<CSOEconItem> items)
    {
        if (items.Count < 1)
        {
            return;
        }
        
        var last = items.Last();
        
        foreach (var item in items)
        {
            UseItem(item.id);
            
            if (item != last)
            {
                await Task.Delay(ItemsUseDelay);
            }
        }
    }
    
    public void DeleteItem(ulong itemId)
    {
        var request = new ClientGCMsg<DeleteMsg>();
        request.Write(itemId);
        
        _steamGameCoordinator.Send(request, AppId);
    }
    
    public async Task DeleteItems(IReadOnlyCollection<CSOEconItem> items)
    {
        if (items.Count < 1)
        {
            return;
        }
        
        var last = items.Last();
        
        foreach (var item in items)
        {
            DeleteItem(item.id);
            
            if (item != last)
            {
                await Task.Delay(ItemsDeleteDelay);
            }
        }
    }
}