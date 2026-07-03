using CounterStrikeSharp.API.Modules.Cvars;

namespace Advertisements;

public static class ConVars
{
	public static FakeConVar<int> ServerIdCvar = new("css_serverid", "The serverId from the CSS-Panel db");
	public static FakeConVar<int> NodeIdCvar = new("sa_node", "The node id this server belongs to (0 = none)");
}
