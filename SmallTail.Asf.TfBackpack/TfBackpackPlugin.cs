using System.Composition;
using System.Reflection;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Integration;

namespace SmallTail.Asf.TfBackpack;

[Export(typeof(IPlugin))]
public class TfBackpackPlugin : IPlugin, IBotCommand2
{
    public string Name => "SmallTail TF Backpack";
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));
    
    public Task OnLoaded()
    {
        ASF.ArchiLogger.LogGenericInfo("SmallTail TF Backpack loaded");
        
        return Task.CompletedTask;
    }
    
    public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0)
    {
        if (args[0] == "tf2slots")
        {
            var commandBot = Bot.GetBot(args[1]);

            if (commandBot is null)
            {
                return $"<{args[1]}> Bot not found";
            }

            var steamLoginSecure = bot.ArchiWebHandler.WebBrowser.CookieContainer.GetCookieValue(ArchiWebHandler.SteamStoreURL, "steamLoginSecure");
            
            if (string.IsNullOrWhiteSpace(steamLoginSecure))
            {
                return $"<{bot.BotName}> Failed to get steamLoginSecure";
            }

            var steamLoginSecureSplit = steamLoginSecure.Split("||");

            var steamId = steamLoginSecureSplit[0];
            var accessToken = steamLoginSecureSplit[1];
            
            var response = await commandBot.ArchiWebHandler.UrlGetToJsonObjectWithSession<GetPlayerItemsResponse>(
                new Uri($"https://api.steampowered.com/IEconItems_440/GetPlayerItems/v1/?access_token={accessToken}&steamid={steamId}"));

            if (response?.Content is null)
            {
                return $"<{commandBot.BotName}> Failed";
            }

            return $"<{commandBot.BotName}> {response.Content.Result.NumBackpackSlots}";
        }

        return null;
    }
}