using System.Collections.Concurrent;
using System.Composition;
using System.Reflection;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using SteamKit2;

namespace SmallTail.Asf.TfBackpack;

[Export(typeof(IPlugin))]
public class Tf2Plugin : IPlugin, IBotCommand2, IBotSteamClient
{
    public string Name => "SmallTail TF2";
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));

    private readonly ConcurrentDictionary<string, Tf2BotHandler> _tf2BotHandlers = new();
    
    public async Task OnLoaded()
    {
        ASF.ArchiLogger.LogGenericInfo("SmallTail TF2 loaded! ^-^");
    }
    
    public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0)
    {
        if (args.Length < 2)
        {
            return null;
        }

        var command = args[0].ToLower();
        var botName = args[1].ToLower();
        
        if (botName == "asf")
        {
            return null;
        }
        
        Func<Bot, Tf2BotHandler, string[], Task<string?>>? handler = command switch
        {
            "tf2slots" => HandleTf2Slots,
            "tf2premium" => HandleTf2Premium,
            
            "tf2useid" => HandleTf2UseId,
            "tf2usedef" => HandleTf2UseDef,
            
            "tf2rm" => HandleTf2Rm,
            "tf2rmdef" => HandleTf2RmDef,
            
            "tf2bec" => HandleTf2ExpanderCount,
            "tf2beu" => HandleTf2ExpanderUse,
            _ => null
        };

        if (handler is null)
        {
            return null;
        }
        
        var commandBot = Bot.BotsReadOnly?
            .FirstOrDefault(b => b.Key.ToLower() == botName)
            .Value;

        if (commandBot is null)
        {
            return $"<{botName}> Bot not found";
        }

        if (!_tf2BotHandlers.TryGetValue(commandBot.BotName, out var tf2BotHandler))
        {
            return $"<{commandBot.BotName}> Failed to get Tf2BotHandler";
        }
        
        return await handler(commandBot, tf2BotHandler, args);
    }

    private async Task<string?> HandleTf2Slots(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        var waiters = await tf2BotHandler.Connect();
        await waiters.AccountLoaded.Task;
        await tf2BotHandler.Disconnect();
            
        return $"<{bot.BotName}> {tf2BotHandler.SlotCount}";
    }

    private async Task<string?> HandleTf2Premium(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        var waiters = await tf2BotHandler.Connect();
        await waiters.AccountLoaded.Task;
        await tf2BotHandler.Disconnect();
            
        return $"<{bot.BotName}> {tf2BotHandler.IsPremium.ToString()}";
    }

    private async Task<string?> HandleTf2UseId(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (args.Length < 3)
        {
            return $"<{bot.BotName}> <itemId> argument is required";
        }
        
        if (!ulong.TryParse(args[2], out var itemId))
        {
            return $"<{args[1]}> Bad item id";
        }

        var waiters = await tf2BotHandler.Connect();
        await waiters.AccountLoaded.Task;
        
        tf2BotHandler.UseItem(itemId);

        await tf2BotHandler.Disconnect();
            
        return $"<{bot.BotName}> Used";
    }

    private async Task<string?> HandleTf2UseDef(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (args.Length < 3)
        {
            return $"<{bot.BotName}> <itemDef> argument is required";
        }
        
        if (!uint.TryParse(args[2], out var defIndex))
        {
            return $"<{args[1]}> Bad item def";
        }
        
        if (args.Length < 4)
        {
            return $"<{bot.BotName}> <count | all> argument is required";
        }

        var count = args[3].ToLower() == "all" ? int.MaxValue : int.Parse(args[3]);

        var waiters = await tf2BotHandler.Connect();
        await waiters.ItemsLoaded.Task;
        
        var items = tf2BotHandler.Items
            .Where(i => i.def_index == defIndex)
            .Take(count)
            .ToList();
        
        await tf2BotHandler.UseItems(items);

        await tf2BotHandler.Disconnect();
            
        return $"<{bot.BotName}> Used {items.Count}";
    }

    private async Task<string?> HandleTf2Rm(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (args.Length < 3)
        {
            return $"<{bot.BotName}> <itemId | all> argument is required";
        }

        var itemIdArg = args[2];

        if (itemIdArg == "all")
        {
            var waiters = await tf2BotHandler.Connect();
            await waiters.ItemsLoaded.Task;

            var originalItemCount = tf2BotHandler.Items.Count;
            var currentItemCount = 0;

            for (var i = 0; i < 3; i++)
            {
                currentItemCount = tf2BotHandler.Items.Count;

                if (currentItemCount == 0)
                {
                    break;
                }
                
                await tf2BotHandler.DeleteItems(tf2BotHandler.Items.ToList());

                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            await tf2BotHandler.Disconnect();
            
            return $"<{bot.BotName}> Item count {originalItemCount} -> {currentItemCount}";
        }
        else
        {
            if (!ulong.TryParse(itemIdArg, out var itemId))
            {
                return $"<{bot.BotName}> Bad item id";
            }
            
            var waiters = await tf2BotHandler.Connect();
            await waiters.AccountLoaded.Task;
        
            tf2BotHandler.DeleteItem(itemId);

            await tf2BotHandler.Disconnect();
            
            return $"<{bot.BotName}> Deleted";
        }
    }

    private async Task<string?> HandleTf2RmDef(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (args.Length < 2)
        {
            return $"<{bot.BotName}> <itemDef > argument is required";
        }

        if (!uint.TryParse(args[2], out var defIndex))
        {
            return $"<{args[1]}> Bad item def";
        }
        
        var waiters = await tf2BotHandler.Connect();
        await waiters.ItemsLoaded.Task;
        
        var items = tf2BotHandler.Items
            .Where(i => i.def_index == defIndex)
            .ToList();

        await tf2BotHandler.DeleteItems(items);

        await tf2BotHandler.Disconnect();

        return $"<{bot.BotName}> Deleted {items.Count}";
    }
    private async Task<string?> HandleTf2ExpanderCount(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        var waiters = await tf2BotHandler.Connect();
        await waiters.ItemsLoaded.Task;
        await tf2BotHandler.Disconnect();
        
        var backpackExtenders = tf2BotHandler.Items
            .Where(i => i.def_index == Tf2Items.BackpackExpander)
            .ToList();

        return $"<{bot.BotName}> {backpackExtenders.Count}";
    }

    private async Task<string?> HandleTf2ExpanderUse(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (args.Length < 3)
        {
            return $"<{bot.BotName}> <count | all> argument is required";
        }

        var count = args[2].ToLower() == "all" ? int.MaxValue : int.Parse(args[2]);
        
        var waiters = await tf2BotHandler.Connect();
        await waiters.ItemsLoaded.Task;
        
        var backpackExtenders = tf2BotHandler.Items
            .Where(i => i.def_index == Tf2Items.BackpackExpander)
            .Take(count)
            .ToList();
        
        await tf2BotHandler.UseItems(backpackExtenders);

        await tf2BotHandler.Disconnect();

        return $"<{bot.BotName}> Used {backpackExtenders.Count}";
    }

    public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager)
    {
        var tfBotHandler = new Tf2BotHandler(bot);

        if (!_tf2BotHandlers.TryAdd(bot.BotName, tfBotHandler))
        {
            return Task.CompletedTask;
        }
        
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(
            c => tfBotHandler.OnGCMessage(c));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot)
    {
        return Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(null);
    }
}