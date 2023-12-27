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
        
        var commandBot = Bot.BotsReadOnly?
            .FirstOrDefault(b => b.Key.ToLower() == args[1].ToLower())
            .Value;

        if (commandBot is null)
        {
            return $"<{args[1]}> Bot not found";
        }

        if (!_tf2BotHandlers.TryGetValue(commandBot.BotName, out var tf2BotHandler))
        {
            return $"<{commandBot.BotName}> Failed to get Tf2BotHandler";
        }
        
        Func<Bot, Tf2BotHandler, string[], Task<string?>>? handler = args[0].ToLower() switch
        {
            "tf2slots" => HandleTf2Slots,
            "tf2premium" => HandleTf2Premium,
            "tf2use" => HandleTf2Use,
            "tf2rm" => HandleTf2Rm,
            "tf2bec" => HandleTf2ExpanderCount,
            "tf2beu" => HandleTf2ExpanderUse,
            _ => null
        };

        if (handler is null)
        {
            return null;
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

    private async Task<string?> HandleTf2Use(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
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

    private async Task<string?> HandleTf2Rm(Bot bot, Tf2BotHandler tf2BotHandler, string[] args)
    {
        if (!ulong.TryParse(args[2], out var itemId))
        {
            return $"<{args[1]}> Bad item id";
        }

        var waiters = await tf2BotHandler.Connect();
        await waiters.AccountLoaded.Task;
        
        tf2BotHandler.DeleteItem(itemId);

        await tf2BotHandler.Disconnect();
            
        return $"<{bot.BotName}> Deleted";
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
            return $"<{bot.BotName}> count argument is required, either a number or all";
        }

        var count = args[2].ToLower() == "all" ? int.MaxValue : int.Parse(args[2]);
        
        var waiters = await tf2BotHandler.Connect();
        await waiters.ItemsLoaded.Task;
        
        var backpackExtenders = tf2BotHandler.Items
            .Where(i => i.def_index == Tf2Items.BackpackExpander)
            .ToList();
        
        foreach (var backpackExtender in backpackExtenders.Take(count))
        {
            tf2BotHandler.UseItem(backpackExtender.id);
            
            if (backpackExtender != backpackExtenders.Last())
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

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