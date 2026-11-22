using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Advertisements;

public class AdvertisementConfig : BasePluginConfig
{
	public string? ChatPrefix { get; set; } = "> {lightblue}CSS{defualt} | ";
	public string? DatabaseHost { get; set; } = "";
	public int DatabasePort { get; set; } = 3306;
	public string? DatabaseUser { get; set; } = "";
	public string? DatabasePassword { get; set; } = "";
	public string? DatabaseName { get; set; } = "";
	// MySqlConnector SslMode: None, Preferred, Required, VerifyCA, VerifyFull. Preferred = TLS when the server offers it.
	public string DatabaseSslMode { get; set; } = "Preferred";
	public float Timer { get; set; } = 25;
	// Only without CS2-SimpleAdmin - with it, the server's id is its `sa_servers` row (see OnAllPluginsLoaded)
	public int ServerId { get; set; } = 0;
	// The HUD strip for location "hud" ads. Optional: a config without it gets these defaults.
	[JsonPropertyName("Hud")]
	public AdvertisementHudConfig Hud { get; set; } = new();
}

/// <summary>"Hud" in Advertisements.json.</summary>
public class AdvertisementHudConfig
{
	/// <summary>The strip for "hud" ads. False (or a strip that failed to start) prints those ads in chat instead.</summary>
	[JsonPropertyName("Enabled")]
	public bool Enabled { get; set; } = true;

	/// <summary>A "hud" ad that comes up during freeze time waits for the round to go live, so it never meets
	/// the Retakes site panel or the buy menu.</summary>
	[JsonPropertyName("WaitForFreezeEnd")]
	public bool WaitForFreezeEnd { get; set; } = true;
}
