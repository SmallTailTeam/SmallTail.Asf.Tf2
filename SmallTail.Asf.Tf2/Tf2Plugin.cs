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
    
    public Task OnLoaded()
    {
        ASF.ArchiLogger.LogGenericInfo("SmallTail TF2 loaded! ^-^");
        
        return Task.CompletedTask;
    }
    
    public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0)
    {
        Func<string[], Task<string?>>? handler = args[0] switch
        {
            "tf2slots" => HandleTf2Slots,
            "tf2premium" => HandleTf2Premium,
            "tf2use" => HandleTf2Use,
            _ => null
        };

        if (handler is null)
        {
            return null;
        }

        return await handler(args);
    }

    private async Task<string?> HandleTf2Slots(string[] args)
    {
        var bot = Bot.GetBot(args[1]);

        if (bot is null)
        {
            return $"<{args[1]}> Bot not found";
        }

        if (!_tf2BotHandlers.TryGetValue(bot.BotName, out var tf2BotHandler))
        {
            return $"<{bot.BotName}> Failed to get Tf2BotHandler";
        }

        var slotCount = await tf2BotHandler.GetSlotCount();
            
        return $"<{bot.BotName}> {slotCount}";
    }

    private async Task<string?> HandleTf2Premium(string[] args)
    {
        var bot = Bot.GetBot(args[1]);

        if (bot is null)
        {
            return $"<{args[1]}> Bot not found";
        }

        if (!_tf2BotHandlers.TryGetValue(bot.BotName, out var tf2BotHandler))
        {
            return $"<{bot.BotName}> Failed to get Tf2BotHandler";
        }

        var isPremium = await tf2BotHandler.GetIsPremium();
            
        return $"<{bot.BotName}> {isPremium.ToString()}";
    }

    private async Task<string?> HandleTf2Use(string[] args)
    {
        var bot = Bot.GetBot(args[1]);

        if (bot is null)
        {
            return $"<{args[1]}> Bot not found";
        }

        if (!ulong.TryParse(args[2], out var itemId))
        {
            return $"<{args[1]}> Bad item id";
        }

        if (!_tf2BotHandlers.TryGetValue(bot.BotName, out var tf2BotHandler))
        {
            return $"<{bot.BotName}> Failed to get Tf2BotHandler";
        }

        await tf2BotHandler.UseItem(itemId);
            
        return $"<{bot.BotName}> Done";
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