using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Advertisements;
[MinimumApiVersion(240)]
public partial class AdvertisementsCore : BasePlugin, IPluginConfig<AdvertisementConfig>
{
	public override string ModuleName => "[CSSPanel] Advertisements";
	public override string ModuleVersion => "2.0";
	public override string ModuleAuthor => "Johnoclock, xWidovV, ShiNxz";
	public override string ModuleDescription => "Dispaly advertisements straight from the css-panel";

	private MySqlDb? g_Db = null;
	readonly HashSet<Advertisement> g_AdvertisementsList = [];
	private CounterStrikeSharp.API.Modules.Timers.Timer? timer;
	public required AdvertisementConfig Config { get; set; }

	public void OnConfigParsed(AdvertisementConfig config)
	{
		Config = config;
	}

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventCsWinPanelRound>(EventCsWinPanelRound, HookMode.Pre);

		if (Config == null)
		{
			Log("Error loading the config");
			return;
		}

		if (Config.DatabaseHost == null || Config.DatabaseHost == "" || Config.DatabaseUser == null || Config.DatabaseUser == "" || Config.DatabasePassword == null || Config.DatabasePassword == "" || Config.DatabaseName == null || Config.DatabaseName == "")
		{
			Log("Please fill in the database information in the config");
			return;
		}

		g_Db = new(Config.DatabaseHost, Config.DatabaseUser, Config.DatabasePassword, Config.DatabaseName, Config.DatabasePort);
		g_Db.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS `{Constants.TABLE_NAME}` (`id` INT NOT NULL AUTO_INCREMENT, `text` TEXT NOT NULL, `location` VARCHAR(128) NOT NULL COMMENT 'chat / center', `servers` TEXT COMMENT 'NULL/blank = all', `flags` VARCHAR(512), `multiply` int(12) NOT NULL DEFAULT 1, `enabled` BOOLEAN NOT NULL DEFAULT TRUE, PRIMARY KEY (`id`));");

		FetchAdvertisements();

		RegisterFakeConVars(typeof(ConVars));

		int serverId = ConVars.ServerIdCvar != null && ConVars.ServerIdCvar.Value != 0 ? ConVars.ServerIdCvar.Value : Config.ServerId;
		Log($"Plugin loaded! ServerId: {serverId}");
	}

	public override void Unload(bool hotReload)
	{
		timer!.Kill();
		Log("Plugin unloaded");
	}
}
