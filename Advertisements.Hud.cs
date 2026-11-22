using Advertisements.Hud;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;

namespace Advertisements;

/// <summary>
/// The plugin side of the HUD strip: location "hud" ads go to <see cref="AdHud"/>, which only draws. Each
/// player's text is built here with their own {NAME} and the rest, the same way the website's admin preview
/// builds it (<see cref="AdHudFormat"/>).
///
/// <para>Off (Hud.Enabled false) or failed to start: HUD ads print in chat, with their formats taken off.</para>
/// </summary>
public partial class AdvertisementsCore
{
	private AdHud? g_Hud;

	/// <summary>A HUD ad that came up in freeze time, shown when the round goes live.</summary>
	private Advertisement? g_PendingHudAd;

	private bool HudOn => Config.Hud.Enabled && g_Hud is { Enabled: true };

	private void HudLoad()
	{
		RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
		RegisterEventHandler<EventPlayerDisconnect>(OnHudPlayerDisconnect);
		RegisterListener<Listeners.OnMapEnd>(OnHudMapEnd);

		if (!Config.Hud.Enabled)
			return;

		g_Hud = new AdHud(this);
		g_Hud.Init();

		if (g_Hud.Enabled)
			AddTimer(0.1f, () => g_Hud?.Tick(), TimerFlags.REPEAT);
	}

	private void HudUnload()
	{
		g_Hud?.Dispose();
		g_Hud = null;
		g_PendingHudAd = null;
	}

	/// <summary>
	/// Holds a HUD ad that came up in freeze time until the round goes live, so the strip never meets the
	/// Retakes site panel or the buy menu. True when the ad was held.
	/// </summary>
	private bool HudHoldForFreezeTime(Advertisement advertisement)
	{
		if (advertisement.Location != "hud" || !HudOn || !Config.Hud.WaitForFreezeEnd || !InFreezeTime())
			return false;

		g_PendingHudAd = advertisement;
		return true;
	}

	private static bool InFreezeTime()
	{
		try
		{
			var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
			return rules?.FreezePeriod == true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
	{
		if (g_PendingHudAd is { } ad)
		{
			g_PendingHudAd = null;
			BroadcastAdvertisement(ad);
		}

		return HookResult.Continue;
	}

	private HookResult OnHudPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		if (@event.Userid is { IsValid: true } player)
			g_Hud?.OnDisconnect(player.Slot);

		return HookResult.Continue;
	}

	private void OnHudMapEnd()
	{
		g_PendingHudAd = null;
		g_Hud?.Clear();
	}

	/// <summary>One player's strip, or the ad in chat when the strip can't reach them.</summary>
	private void DisplayHudAdvertisement(CCSPlayerController player, Advertisement advertisement)
	{
		var content = AdHudLayout.Build(advertisement.Text, advertisement.Hud ?? AdHudStyle.Default, MessageVariables(player));

		if (HudOn && g_Hud!.Show(player, content))
			return;

		var line = content.Title.Length > 0 ? $"{content.Title} - {content.PlainText}" : content.PlainText;
		if (line.Length > 0)
			player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} {line}");
	}

	/// <summary>The {VARIABLES} an ad can use, for one player: the chat ads' set, plus {MAPNAME} as the website names it.</summary>
	private static Dictionary<string, string> MessageVariables(CCSPlayerController player)
	{
		var map = Server.MapName;

		return new Dictionary<string, string>
		{
			["CURRENTMAP"] = map,
			["MAPNAME"] = map,
			["TIME"] = DateTime.Now.ToString("HH:mm:ss"),
			["DATE"] = DateTime.Now.ToString("dd.MM.yyyy"),
			["SERVERNAME"] = ConVar.Find("hostname")?.StringValue ?? "",
			["NAME"] = player.PlayerName,
			["STEAMID"] = player.SteamID.ToString(),
			["PLAYERCOUNT"] = Utilities.GetPlayers().FindAll(x => ValidClient(x)).Count.ToString(),
			["MAXPLAYERS"] = Server.MaxPlayers.ToString(),
			["IP"] = ConVar.Find("ip")?.StringValue ?? "",
			["PORT"] = ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString() ?? "",
		};
	}
}
