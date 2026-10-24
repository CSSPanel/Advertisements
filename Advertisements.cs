using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using CS2_SimpleAdminApi;
using Nexd.MySQL;

namespace Advertisements;
[MinimumApiVersion(240)]
public partial class AdvertisementsCore : BasePlugin, IPluginConfig<AdvertisementConfig>
{
	public override string ModuleName => "[CSSPanel] Advertisements";
	public override string ModuleVersion => "2.1";
	public override string ModuleAuthor => "Johnoclock, xWidovV, ShiNxz";
	public override string ModuleDescription => "Dispaly advertisements straight from the css-panel";

	private MySqlDb? g_Db = null;
	readonly HashSet<Advertisement> g_AdvertisementsList = [];
	private CounterStrikeSharp.API.Modules.Timers.Timer? timer;
	public required AdvertisementConfig Config { get; set; }

	private static readonly PluginCapability<ICS2_SimpleAdminApi?> SimpleAdminCapability = new("simpleadmin:api");
	private ICS2_SimpleAdminApi? g_SimpleAdmin = null;

	// The server's `sa_servers` id, which the ads are targeted by. CS2-SimpleAdmin finds (or creates) that
	// row by the server's address, so it is asked instead of a per-server convar. 0 until it is known:
	// only the ads for every server are shown then.
	private int g_ServerId = 0;
	private CounterStrikeSharp.API.Modules.Timers.Timer? serverIdTimer;

	// How long to wait for CS2-SimpleAdmin's database lookup before giving up on targeted ads
	private const int SERVER_ID_WAIT_SECONDS = 60;

	public void OnConfigParsed(AdvertisementConfig config)
	{
		Config = config;
	}

	public override void Load(bool hotReload)
	{
		RegisterEventHandler<EventCsWinPanelRound>(EventCsWinPanelRound, HookMode.Pre);
		RegisterFakeConVars(typeof(ConVars));

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
		g_Db.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS `{Constants.TABLE_NAME}` (`id` INT NOT NULL AUTO_INCREMENT, `text` TEXT NOT NULL, `location` VARCHAR(128) NOT NULL COMMENT 'chat / center', `servers` TEXT COMMENT 'NULL/blank = all DEFAULT NULL', `groups` TEXT, `flags` VARCHAR(512), `multiply` int(12) NOT NULL DEFAULT 1, `enabled` BOOLEAN NOT NULL DEFAULT TRUE, `nodes` TEXT COMMENT 'NULL/blank = all, or comma-separated node ids', PRIMARY KEY (`id`));");

		Log("Plugin loaded");
	}

	// The ads are fetched once CS2-SimpleAdmin says which server this is - it may load after this plugin
	public override void OnAllPluginsLoaded(bool hotReload)
	{
		try
		{
			g_SimpleAdmin = SimpleAdminCapability.Get();
		}
		catch (Exception)
		{
			g_SimpleAdmin = null;
		}

		if (g_SimpleAdmin == null)
		{
			g_ServerId = Config.ServerId;
			Log($"CS2-SimpleAdmin not found - using ServerId {g_ServerId} from the config");
			FetchAdvertisements();
			return;
		}

		WaitForServerId();
	}

	/// <summary>
	/// CS2-SimpleAdmin reads its `sa_servers` row in the background, and raises OnSimpleAdminReady as
	/// that read starts rather than when it ends - so its id is polled until it is there.
	/// </summary>
	private void WaitForServerId()
	{
		serverIdTimer?.Kill();

		int waited = 0;
		serverIdTimer = AddTimer(1.0f, () =>
		{
			int? id = g_SimpleAdmin?.GetServerId();

			if (id is > 0)
			{
				serverIdTimer?.Kill();
				g_ServerId = id.Value;
				Log($"ServerId {g_ServerId} from CS2-SimpleAdmin");
				FetchAdvertisements();
				return;
			}

			if (++waited < SERVER_ID_WAIT_SECONDS) return;

			serverIdTimer?.Kill();
			Log($"CS2-SimpleAdmin had no server id after {SERVER_ID_WAIT_SECONDS}s - showing only the ads for every server");
			FetchAdvertisements();
		}, TimerFlags.REPEAT);
	}

	/// <summary>The id the ads are picked by: CS2-SimpleAdmin's, read again in case it was not known yet.</summary>
	private int CurrentServerId()
	{
		int? id = g_SimpleAdmin?.GetServerId();
		if (id is > 0) g_ServerId = id.Value;

		return g_ServerId;
	}

	public override void Unload(bool hotReload)
	{
		timer?.Kill();
		serverIdTimer?.Kill();
		Log("Plugin unloaded");
	}
}

