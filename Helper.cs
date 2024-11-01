using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;
using CounterStrikeSharp.API;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Admin;

namespace Advertisements;

public partial class AdvertisementsCore
{
	private void FetchAdvertisements()
	{
		// Reset the advertisements list
		g_AdvertisementsList.Clear();

		// Stop the timer if it's running
		timer?.Kill();

		int serverId = ConVars.ServerIdCvar != null && ConVars.ServerIdCvar.Value != 0 ? ConVars.ServerIdCvar.Value : Config.ServerId;
		Log($"Fetching advertisements for serverId: {serverId}");

		if (g_Db == null)
		{
			Log("Database connection is not initialized.");
			return;
		}

		var results = g_Db.ExecuteQuery($"SELECT * FROM `{Constants.TABLE_NAME}` WHERE FIND_IN_SET({serverId}, `servers`) OR `servers` IS NULL OR `servers` = ''");

		if (results == null)
		{
			Log("Query returned no results.");
			return;
		}

		foreach (KeyValuePair<int, MySqlFieldValue> pair in results)
		{
			if (pair.Value == null)
			{
				Log("Encountered a null value in the results.");
				continue;
			}

			string enabled = pair.Value["enabled"]?.ToString() ?? "";
			if (enabled == "0") continue;

			string text = pair.Value["text"]?.ToString() ?? "";
			string location = pair.Value["location"]?.ToString() ?? "";
			string flags = pair.Value["flags"]?.ToString() ?? "";
			int multiply = int.TryParse(pair.Value["multiply"]?.ToString(), out int parsedChance) ? parsedChance : 1;

			// For each multiply, add the advertisement to the list
			for (int i = 0; i < multiply; i++)
				g_AdvertisementsList.Add(new Advertisement(text, location, flags));
		}

		// filter the not enabled advertisements
		Log($"Loaded {g_AdvertisementsList.Count()} advertisements (ad * multiply).");
		timer = AddTimer(Config.Timer, Timer_Advertisements, TimerFlags.REPEAT);
	}

	Advertisement? selectedAd = null;

	private void Timer_Advertisements()
	{

		if (g_AdvertisementsList == null || g_AdvertisementsList.Count < 1)
		{
			Log("No advertisements to display.");
			return;
		}

		Random random = new();
		selectedAd = g_AdvertisementsList.ElementAt(random.Next(0, g_AdvertisementsList.Count));

		foreach (CCSPlayerController player in Utilities.GetPlayers())
		{
			if (!ValidClient(player)) continue;

			if (!string.IsNullOrEmpty(selectedAd.Flag) && !AdminManager.PlayerHasPermissions(player, selectedAd.Flag.Split(','))) continue;

			DisplayAdvertisement(player, selectedAd);
		}
	}

	private void DisplayAdvertisement(CCSPlayerController player, Advertisement advertisement)
	{
		// Ensure both player and advertisement are valid
		if (player == null || advertisement == null)
		{
			Log("Invalid player or advertisement.");
			return;
		}

		// Display the advertisement based on its specified location
		switch (advertisement.Location)
		{
			case "chat":
				// Display the advertisement in chat
				player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} {ReplaceMessageTags(advertisement.Text, player)}");
				break;

			case "center":
				// Display the advertisement in the center of the screen
				player.PrintToCenter($" {ModifyColorValue(Config.ChatPrefix!)} {ReplaceMessageTags(advertisement.Text, player)}");
				break;

			default:
				// Handle unknown locations, perhaps log an error or ignore
				Log($"Unknown advertisement location: {advertisement.Location}");
				break;
		}
	}

	// Essential method for replacing chat colors from the config file, the method can be used for other things as well.
	public static string ModifyColorValue(string msg)
	{
		if (!msg.Contains('{'))
		{
			return string.IsNullOrEmpty(msg) ? "" : msg;
		}

		string modifiedValue = msg;

		foreach (FieldInfo field in typeof(ChatColors).GetFields())
		{
			string pattern = $"{{{field.Name}}}";
			if (msg.Contains(pattern, StringComparison.OrdinalIgnoreCase))
			{
				modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
			}
		}
		return modifiedValue;
	}

	public class Advertisement(string message, string location, string flag)
	{
		public string Text { get; set; } = message;
		public string Location { get; set; } = location;
		public string Flag { get; set; } = flag;
	}

	private static bool ValidClient(CCSPlayerController player)
	{
		if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || !player.PlayerPawn.IsValid || player.UserId == -1 || player.IsBot || player.Slot == 65535) return false;
		return true;
	}

	private static string ReplaceMessageTags(string message, CCSPlayerController player)
	{
		// Replace various tags with corresponding values
		message = message
			.Replace("{CURRENTMAP}", Server.MapName)
			.Replace("{TIME}", DateTime.Now.ToString("HH:mm:ss"))
			.Replace("{DATE}", DateTime.Now.ToString("dd.MM.yyyy"))
			.Replace("{SERVERNAME}", ConVar.Find("hostname")!.StringValue)
			.Replace("{NAME}", player.PlayerName)
			.Replace("{STEAMID}", player.SteamID.ToString())
			.Replace("{PLAYERCOUNT}", Utilities.GetPlayers().FindAll(x => ValidClient(x)).Count.ToString())
			.Replace("{MAXPLAYERS}", Server.MaxPlayers.ToString())
			.Replace("{IP}", ConVar.Find("ip")!.StringValue)
			.Replace("{PORT}", ConVar.Find("hostport")!.GetPrimitiveValue<int>().ToString())
			.Replace("\\n", "\u2029")
			.Replace("\n", "\u2029");

		return ModifyColorValue(message);
	}

	public static void Log(string message)
	{
		Console.WriteLine($"[CSS Advertisements] {message}");
	}
}
