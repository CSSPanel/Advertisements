using CounterStrikeSharp.API.Modules.Cvars;

namespace Advertisements;

public static class ConVars
{
	public static FakeConVar<int> ServerIdCvar = new("css_serverid", "The serverId from the CSS-Panel db");
}
