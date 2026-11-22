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
/// <para><b>Their own timer.</b> HUD ads are not in the chat and center rotation: they have their own list
/// (multiply works the same) and their own timer, every Hud.IntervalSeconds, next to the Timer one, so a chat
/// line and a strip can come at the same moment. A tick that finds the last strip still on screen is skipped,
/// never stacked.</para>
///
/// <para>Off (Hud.Enabled false) or failed to start: HUD ads print in chat, with their formats taken off, and
/// go back into the chat rotation, so they do not become a second stream of chat lines.</para>
/// </summary>
public partial class AdvertisementsCore
{
	private AdHud? g_Hud;

	/// <summary>The HUD ads' rotation: each ad as many times as its multiply, like g_AdvertisementsList.</summary>
	readonly List<Advertisement> g_HudAdvertisementsList = [];
	private CounterStrikeSharp.API.Modules.Timers.Timer? hudTimer;

	/// <summary>A HUD ad that came up in freeze time, shown when the round goes live.</summary>
	private Advertisement? g_PendingHudAd;

	/// <summary>Server time the last strip is off the screen: its duration plus the slide in and out.</summary>
	private float g_HudBusyUntil;

	/// <summary>The strip's slide in and out, on top of the ad's duration (AdHud's EntrySeconds and ExitSeconds).</summary>
	private const float HudTransitionSeconds = 0.7f;

	private const float DefaultHudInterval = 20f;

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
		hudTimer?.Kill();
		hudTimer = null;
		g_Hud?.Dispose();
		g_Hud = null;
		g_PendingHudAd = null;
	}

	/// <summary>
	/// Starts the HUD ads' timer, from FetchAdvertisements, right after the chat one: the same lifecycle, so
	/// css_adv_sync and a plugin reload restart both. Like the chat timer it is not stopped on map change.
	/// </summary>
	private void StartHudTimer()
	{
		hudTimer?.Kill();
		hudTimer = null;

		if (g_HudAdvertisementsList.Count == 0)
			return;

		var interval = Config.Hud.IntervalSeconds > 0 ? Config.Hud.IntervalSeconds : DefaultHudInterval;
		hudTimer = AddTimer(Math.Max(1f, interval), Timer_HudAdvertisements, TimerFlags.REPEAT);
	}

	private void Timer_HudAdvertisements()
	{
		if (g_HudAdvertisementsList.Count == 0 || !HudOn)
			return;

		// The last strip is still up (an interval shorter than duration + slide): skip this tick, never stack.
		// A busy time far ahead is from before a map change reset the clock.
		var now = Server.CurrentTime;
		if (now < g_HudBusyUntil && g_HudBusyUntil - now <= AdHudFormat.DurationMax + HudTransitionSeconds + 1f)
			return;

		var ad = g_HudAdvertisementsList[Random.Shared.Next(g_HudAdvertisementsList.Count)];

		// In freeze time it waits for the round to go live (EventRoundFreezeEnd)
		if (HudHoldForFreezeTime(ad))
			return;

		BroadcastHudAdvertisement(ad);
	}

	private void BroadcastHudAdvertisement(Advertisement ad)
	{
		g_HudBusyUntil = Server.CurrentTime + (ad.Hud ?? AdHudStyle.Default).Duration + HudTransitionSeconds;
		BroadcastAdvertisement(ad);
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
			BroadcastHudAdvertisement(ad);
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
		g_HudBusyUntil = 0;
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
