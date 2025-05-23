﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace Advertisements;

public partial class AdvertisementsCore
{
	// [ConsoleCommand("css_adv", "advertisements command")]
	// [ConsoleCommand("css_advertisements", "advertisements command")]
	// [RequiresPermissionsOr("@css/slay", "@adv/adv", "@adv/root")]
	// public void Command_Adv(CCSPlayerController player, CommandInfo command)
	// {
	//     if (player == null || !ValidClient(player)) return;

	//     if (command.ArgCount == 1)
	//     {
	//         player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Please provide one argment");
	//         player.PrintToChat("- reload");
	//         player.PrintToChat("- add");
	//         player.PrintToChat("- edit");
	//         player.PrintToChat("- remove or delete");
	//         player.PrintToChat("- list");
	//     }
	// }

	// [ConsoleCommand("css_adv_add")]
	// [ConsoleCommand("css_advertisements_add", "advertisements command")]
	// [RequiresPermissionsOr("@css/slay", "@adv/add", "@adv/root")]
	// public void Command_AddAdv(CCSPlayerController player, CommandInfo command)
	// {
	//     if (command.ArgCount < 3 || command.ArgCount > 5)
	//     {
	//         player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Usage: css_advertisements add <message> <location> [<port>]");
	//         return;
	//     }

	//     g_Db!.ExecuteNonQueryAsync($"INSERT INTO `advertisements` (`message`, `location`, server) VALUES ('{command.ArgByIndex(2)}', '{command.ArgByIndex(3)}', '{command.ArgByIndex(4)}');");

	//     ReloadAdvertisements();

	//     player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Successfully added {command.ArgByIndex(2)}");
	// }

	// [ConsoleCommand("css_adv_edit")]
	// [ConsoleCommand("css_advertisements_edit", "advertisements command")]
	// [RequiresPermissionsOr("@css/slay", "@adv/edit", "@adv/root")]
	// public void Command_EditAdv(CCSPlayerController player, CommandInfo command)
	// {
	//     if (command.ArgCount < 3 || command.ArgCount > 4)
	//     {
	//         player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Usage: css_advertisements edit <id> <message>");
	//         return;
	//     }

	//     g_Db!.ExecuteNonQueryAsync($"UPDATE `advertisements` SET message = '{command.ArgByIndex(3)}' WHERE id = '{command.ArgByIndex(2)}'");

	//     ReloadAdvertisements();

	//     player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Successfully edit {command.ArgByIndex(2)}");
	// }

	// [ConsoleCommand("css_adv_delete")]
	// [ConsoleCommand("css_adv_remove")]
	// [ConsoleCommand("css_advertisements_remove", "advertisements command")]
	// [ConsoleCommand("css_advertisements_delete", "advertisements command")]
	// [RequiresPermissionsOr("@css/slay", "@adv/delete", "@adv/root")]
	// public void Command_DelAdv(CCSPlayerController player, CommandInfo command)
	// {
	//     if (command.ArgCount != 3)
	//     {
	//         player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Usage: css_advertisements remove <id>");
	//         return;
	//     }

	//     g_Db!.ExecuteNonQueryAsync($"DELETE FROM `advertisements` WHERE `id` = '{command.ArgByIndex(2)}';");

	//     ReloadAdvertisements();

	//     player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} Successfully Removed {command.ArgByIndex(2)}");
	// }

	[ConsoleCommand("css_adv_sync", "Sync advertisements from the database.")]
	[RequiresPermissions("@css/root")]
	public void Command_ReloadAdv(CCSPlayerController? caller, CommandInfo command)
	{
		FetchAdvertisements();
		// command.ReplyToCommand($"[CSSP] Advertisements reloaded!");
	}

	[ConsoleCommand("css_fexec")]
	[CommandHelper(minArgs: 2, usage: "<#userid or name or steamid> <command>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
	[RequiresPermissions("@css/root")]
	public void OnFexecCommand(CCSPlayerController? caller, CommandInfo command)
	{
		var target = command.GetArg(1);
		var exec = command.GetArg(2);

		List<CCSPlayerController> playersToTarget = GetValidPlayers();

		// Find the player by name, userid or steamid
		if (target.StartsWith("#"))
		{
			playersToTarget = playersToTarget.Where(player => player.UserId.ToString() == target.Replace("#", "")).ToList();
		}
		else if (IsValidSteamId64(target))
		{
			playersToTarget = playersToTarget.Where(player => player.SteamID.ToString() == target).ToList();
		}
		else
		{
			playersToTarget = playersToTarget.Where(player => player.PlayerName.ToLower().Contains(target.ToLower())).ToList();
		}

		playersToTarget.ForEach(player =>
		{
			// player.ExecuteClientCommand(exec);
			player.ExecuteClientCommandFromServer(exec);
		});
	}

	// [ConsoleCommand("css_adv_list")]
	// [ConsoleCommand("css_advertisements_list", "advertisements command")]
	// [RequiresPermissionsOr("@css/slay", "@adv/list", "@adv/root")]
	// public void Command_ListAdv(CCSPlayerController player, CommandInfo command)
	// {
	//     player.PrintToChat($" {ModifyColorValue(Config.ChatPrefix!)} See Console for the list");


	//     var results = g_Db!.ExecuteQuery("select * from advertisements");

	//     foreach (KeyValuePair<int, MySqlFieldValue> pair in results)
	//     {
	//         // Assuming 'id' is the column name for the ID in your database
	//         string id = pair.Value["id"]!.ToString();
	//         string location = pair.Value["location"]!.ToString();
	//         string message = pair.Value["message"]!.ToString();
	//         string server = pair.Value["server"]!.ToString();
	//         if (string.IsNullOrEmpty(server)) server = "any";
	//         string enable = pair.Value["enable"]!.ToString();
	//         string flag = pair.Value["flag"]!.ToString();
	//         if (string.IsNullOrEmpty(flag)) flag = "all";

	//         // Print both ID and message to the console
	//         player.PrintToConsole("ID: " + id + ", Message: " + message + ", Location: " + location + ", Port: " + server + ", Flag: " + flag + ", Enable: " + enable + "\n");
	//     }
	// }
}
