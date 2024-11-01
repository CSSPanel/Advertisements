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
	public float Timer { get; set; } = 25;
	public int ServerId { get; set; } = 1;
}
