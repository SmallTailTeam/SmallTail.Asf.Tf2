using System.ComponentModel.Composition;
using System.Reflection;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;

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
        if (args[0] == "tfbackpackslots")
        {
            var commandBot = Bot.GetBot(args[1]);

            if (commandBot is null)
            {
                return $"<{args[1]}> Bot not found";
            }

            var steamLoginSecureCookie = commandBot.ArchiWebHandler.WebBrowser.CookieContainer
                .GetAllCookies()
                .FirstOrDefault(c => c.Name == "steamLoginSecure");

            if (steamLoginSecureCookie is null || string.IsNullOrWhiteSpace(steamLoginSecureCookie.Value))
            {
                return $"<{commandBot.BotName}> Failed to get steamLoginSecure cookie";
            }

            var steamLoginSecureSplit = steamLoginSecureCookie.Value.Split("%7C%7C");

            if (steamLoginSecureSplit.Length < 2)
            {
                return $"<{commandBot.BotName}> Bad steamLoginSecure cookie";
            }
            
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