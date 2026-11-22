using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using PanoramaManager;

namespace Advertisements.Hud;

/// <summary>
/// The HUD ad strip: ads with location "hud", over the chat or under the score, for the ad's 3-15 seconds with
/// a bar draining along its bottom edge. The layout is hud/panorama/layout/custom_game/ads_hud.xml, shipped in
/// the workshop addon.
///
/// <para><b>One layout, opened once per player and left open.</b> Its root is an invisible full-screen panel;
/// the strip inside carries its own "show", and "hidden" (collapse) while no ad is on. It is un-collapsed a
/// moment before it shows, so the slide-in has a resting place to start from.</para>
///
/// <para><b>Per viewer.</b> {NAME} and the rest differ per player, so every text and class is written with
/// SetVariableFor / SetClassFor. What was written is remembered per viewer and only changes are sent: the
/// strip is a pool of 32 Labels with a colour palette each, and rewriting all of it every 25 seconds for
/// every player would be thousands of class writes for nothing.</para>
///
/// <para><b>Everything is drawn from state.</b> PanoramaManager rebuilds a player's panel after a respawn, a
/// round restart or a map change and drops every per-viewer variable and class (PanelAction.Restored), so the
/// remembered writes are forgotten and <see cref="Restore"/> writes the strip back, with the bar where it
/// should be. Every timed step is a time in the viewer's record, run by <see cref="Tick"/>.</para>
/// </summary>
internal sealed class AdHud : IDisposable
{
	private const string Layout = "panorama/layout/custom_game/ads_hud.vxml_c";

	private const string StripId = "ad_strip";
	private const string BarId = "ad_bar";
	private const string TitleVar = "ad_title";

	/// <summary>The exit transition, before the strip collapses or comes back with the next ad.</summary>
	private const float ExitSeconds = 0.35f;

	/// <summary>
	/// From un-collapsing (and snapping the bar back) to "show" and "run". A class turned off and on again
	/// sooner than about 0.3s does not reach the client, and the slide needs its resting offset seen first.
	/// </summary>
	private const float EntrySeconds = 0.35f;

	/// <summary>The at-N classes: where the bar starts, in 5% steps. at-20 (full) is the stylesheet's default.</summary>
	private const int BarSteps = 20;

	/// <summary>The dur-N classes ads_hud.css defines, in seconds.</summary>
	private const int MaxBarSeconds = 15;

	/// <summary>A restored strip with less than this left is not brought back.</summary>
	private const float MinRestoreSeconds = 1.0f;

	private enum Phase { Idle, Entering, Shown, Leaving }

	private sealed class Viewer
	{
		public Phase Phase = Phase.Idle;

		/// <summary>The ad on the strip, or on its way in or out.</summary>
		public AdHudContent? Content;

		/// <summary>The ad that shows once the one on screen has left.</summary>
		public AdHudContent? Next;

		public float ShowAt = -1;
		public float HideAt = -1;
		public float CollapseAt = -1;

		/// <summary>How long the strip stays once "show" is on: the ad's duration, or what was left after a restore.</summary>
		public float Seconds;

		// What was written to this viewer's panel. Unknown entries are written; known ones only when they change.
		public readonly Dictionary<string, string> Vars = [];
		public readonly Dictionary<(string Id, string Class), bool> Classes = [];

		/// <summary>One-of-a-group classes (position, accent, icon, a run's colour, the bar's dur-N and at-N): the one that is on.</summary>
		public readonly Dictionary<(string Id, string Group), string> Groups = [];

		public void Forget()
		{
			Vars.Clear();
			Classes.Clear();
			Groups.Clear();
		}
	}

	private static readonly string[] LineIds =
		Enumerable.Range(0, AdHudLayout.Lines).Select(l => $"ad_l{l}").ToArray();

	private static readonly string[,] RunIds = Ids("ad_r");
	private static readonly string[,] RunVars = Ids("ad_t");

	private static string[,] Ids(string prefix)
	{
		var ids = new string[AdHudLayout.Lines, AdHudLayout.RunsPerLine];
		for (var l = 0; l < AdHudLayout.Lines; l++)
			for (var r = 0; r < AdHudLayout.RunsPerLine; r++)
				ids[l, r] = $"{prefix}{l}_{r}";
		return ids;
	}

	private readonly BasePlugin _plugin;
	private readonly Dictionary<int, Viewer> _viewers = [];
	private PanelHandle? _panel;

	public AdHud(BasePlugin plugin)
	{
		_plugin = plugin;
	}

	public bool Enabled => _panel != null;

	/// <summary>Spawns the layout once. A second Spawn would orphan the first entity, so this is guarded.</summary>
	public void Init()
	{
		if (_panel != null)
			return;

		try
		{
			Panorama.Init(_plugin);
			_panel = Panorama.Spawn(Layout, new LayoutContract
			{
				// Unique server-wide: dialog variables are keyed by panel id
				RootPanelId = "AdsRoot",
				RevealClass = "show",
				RowCount = 0,
				CaptureInput = false,
				HideHud = HideHudFlags.None,
				SharedText = false,
				SpectatingMessage = null,
			});
			_panel.OnEvent += OnPanelEvent;
			_plugin.Logger.LogInformation("[CSS Advertisements] HUD strip ready");
		}
		catch (Exception ex)
		{
			// HUD ads fall back to chat while Enabled is false
			_plugin.Logger.LogWarning("[CSS Advertisements] HUD strip failed to start, HUD ads go to chat: {Message}", ex.Message);
			_panel = null;
		}
	}

	public void Dispose()
	{
		_panel?.Dispose();
		_panel = null;
		_viewers.Clear();

		try
		{
			Panorama.Shutdown();
		}
		catch (Exception)
		{
			// Unloading anyway
		}
	}

	private void OnPanelEvent(PanelEvent e)
	{
		if (e.Action == PanelAction.Restored && e.Player.IsValid)
			Restore(e.Player);
	}

	// ------------------------------------------------------------------ viewers

	private static bool IsHuman(CCSPlayerController? player) =>
		player is { IsValid: true, IsBot: false, IsHLTV: false } && player.Connected == PlayerConnectedState.Connected;

	private Viewer ViewerOf(CCSPlayerController player)
	{
		if (!_viewers.TryGetValue(player.Slot, out var viewer))
		{
			viewer = new Viewer();
			_viewers[player.Slot] = viewer;
		}

		return viewer;
	}

	/// <summary>Opens the player's session if it is not open yet. A new session starts from the layout as authored.</summary>
	private bool EnsureOpen(CCSPlayerController player)
	{
		if (_panel == null || !IsHuman(player))
			return false;

		if (!_panel.IsOpenFor(player))
		{
			_panel.Open(player);

			var viewer = ViewerOf(player);
			viewer.Forget();
			viewer.Phase = Phase.Idle;
			viewer.Content = null;
			viewer.Next = null;
		}

		return _panel.IsOpenFor(player);
	}

	public void OnDisconnect(int slot)
	{
		_viewers.Remove(slot);
		_panel?.Close(slot);
	}

	/// <summary>Map end: the clock starts over on the next map, so every stored time is meaningless now.</summary>
	public void Clear()
	{
		foreach (var viewer in _viewers.Values)
		{
			viewer.Phase = Phase.Idle;
			viewer.Content = null;
			viewer.Next = null;
			viewer.ShowAt = viewer.HideAt = viewer.CollapseAt = -1;
			viewer.Forget();
		}
	}

	// ------------------------------------------------------------------ showing an ad

	/// <summary>
	/// Puts an ad on the player's strip. A strip that is showing another ad lets it leave first. Returns false
	/// when the strip can't reach this player, so the caller prints the ad in chat.
	/// </summary>
	public bool Show(CCSPlayerController player, AdHudContent content)
	{
		if (!EnsureOpen(player))
			return false;

		var viewer = ViewerOf(player);
		var now = Server.CurrentTime;

		switch (viewer.Phase)
		{
			case Phase.Idle:
			case Phase.Entering:
				// Not on screen yet: write the new one in its place
				Prepare(player, viewer, content, BarSteps, content.Duration, now);
				break;

			case Phase.Shown:
				Leave(player, viewer, now);
				viewer.Next = content;
				break;

			case Phase.Leaving:
				viewer.Next = content;
				break;
		}

		return true;
	}

	/// <summary>
	/// Writes an ad onto the strip while it is out of sight, snaps the bar to where it starts and un-collapses
	/// the strip; <see cref="Tick"/> turns "show" and the bar on after <see cref="EntrySeconds"/>.
	/// </summary>
	private void Prepare(CCSPlayerController player, Viewer viewer, AdHudContent content, int barAt, float seconds, float now)
	{
		viewer.Content = content;
		viewer.Seconds = seconds;

		BarSnap(player, viewer, barAt);
		Write(player, viewer, content);
		Cls(player, viewer, StripId, "show", false);
		Cls(player, viewer, StripId, "hidden", false);

		viewer.Phase = Phase.Entering;
		viewer.ShowAt = now + EntrySeconds;
		viewer.HideAt = -1;
		viewer.CollapseAt = -1;
	}

	private void Leave(CCSPlayerController player, Viewer viewer, float now)
	{
		Cls(player, viewer, StripId, "show", false);
		viewer.Phase = Phase.Leaving;
		viewer.ShowAt = -1;
		viewer.HideAt = -1;
		viewer.CollapseAt = now + ExitSeconds;
	}

	/// <summary>
	/// Writes the strip back for one player after the library rebuilt their panel. Nothing written before is
	/// trusted. A strip that was showing comes back with the time it had left, its bar starting part way.
	/// </summary>
	public void Restore(CCSPlayerController player)
	{
		if (_panel == null || !_panel.IsOpenFor(player) || !_viewers.TryGetValue(player.Slot, out var viewer))
			return;

		var now = Server.CurrentTime;

		// Classes this viewer had on may have survived the rebuild; take them off before forgetting them
		foreach (var (key, cls) in viewer.Groups)
			_panel.SetClassFor(player, key.Id, cls, false);
		viewer.Forget();

		switch (viewer.Phase)
		{
			case Phase.Shown when viewer.Content != null && viewer.HideAt - now - EntrySeconds >= MinRestoreSeconds:
			{
				var left = viewer.HideAt - now - EntrySeconds;
				var at = (int)Math.Round(Math.Clamp(left / Math.Max(viewer.Content.Duration, 1), 0f, 1f) * BarSteps);
				Prepare(player, viewer, viewer.Content, at, left, now);
				return;
			}

			case Phase.Entering when viewer.Content != null:
				Prepare(player, viewer, viewer.Content, BarSteps, viewer.Seconds, now);
				return;

			case Phase.Leaving when viewer.Next != null:
			{
				var next = viewer.Next;
				viewer.Next = null;
				Prepare(player, viewer, next, BarSteps, next.Duration, now);
				return;
			}
		}

		viewer.Phase = Phase.Idle;
		viewer.Content = null;
		viewer.Next = null;
		Cls(player, viewer, StripId, "show", false);
		Cls(player, viewer, StripId, "hidden", true);
	}

	// ------------------------------------------------------------------ the clock

	/// <summary>Runs every timed step. The plugin calls this ten times a second.</summary>
	public void Tick()
	{
		if (_panel == null || _viewers.Count == 0)
			return;

		var now = Server.CurrentTime;

		foreach (var (slot, viewer) in _viewers)
		{
			if (viewer.Phase == Phase.Idle)
				continue;

			var player = Utilities.GetPlayerFromSlot(slot);
			if (player is not { IsValid: true } || !_panel.IsOpenFor(player))
				continue;

			switch (viewer.Phase)
			{
				case Phase.Entering when now >= viewer.ShowAt:
					Cls(player, viewer, StripId, "show", true);
					BarRun(player, viewer, viewer.Seconds);
					viewer.Phase = Phase.Shown;
					viewer.ShowAt = -1;
					viewer.HideAt = now + viewer.Seconds;
					break;

				case Phase.Shown when now >= viewer.HideAt:
					Leave(player, viewer, now);
					break;

				case Phase.Leaving when now >= viewer.CollapseAt:
					if (viewer.Next is { } next)
					{
						viewer.Next = null;
						Prepare(player, viewer, next, BarSteps, next.Duration, now);
						break;
					}

					viewer.Phase = Phase.Idle;
					viewer.Content = null;
					viewer.CollapseAt = -1;
					Cls(player, viewer, StripId, "hidden", true);
					// Out of sight now, so the bar can go back to full for the next ad
					BarSnap(player, viewer, BarSteps);
					break;
			}
		}
	}

	// ------------------------------------------------------------------ the bar

	/// <summary>
	/// Puts the bar at a step with no transition. The duration comes off with "run" in the same write, so the
	/// bar snaps instead of running back to full.
	/// </summary>
	private void BarSnap(CCSPlayerController player, Viewer viewer, int at)
	{
		Group(player, viewer, BarId, "dur", null);
		Cls(player, viewer, BarId, "run", false);
		Group(player, viewer, BarId, "at", at >= BarSteps ? null : $"at-{Math.Max(0, at)}");
	}

	/// <summary>The drain: one linear clip transition to 0 over the time the strip stays.</summary>
	private void BarRun(CCSPlayerController player, Viewer viewer, float seconds)
	{
		var dur = Math.Clamp((int)Math.Round(seconds), 1, MaxBarSeconds);
		Group(player, viewer, BarId, "dur", $"dur-{dur}");
		Cls(player, viewer, BarId, "run", true);
	}

	// ------------------------------------------------------------------ drawing

	/// <summary>The ad's text and classes, for what the viewer does not have yet.</summary>
	private void Write(CCSPlayerController player, Viewer viewer, AdHudContent content)
	{
		Group(player, viewer, StripId, "pos", $"pos-{content.Position}");
		Group(player, viewer, StripId, "ac", $"ac-{content.Accent}");
		Group(player, viewer, StripId, "ic", content.Icon == null ? "no-icon" : $"ic-{content.Icon}");
		Cls(player, viewer, StripId, "big", content.Title.Length > 0);
		Cls(player, viewer, StripId, "rtl", content.Rtl);
		Var(player, viewer, TitleVar, content.Title);

		for (var l = 0; l < AdHudLayout.Lines; l++)
		{
			var line = l < content.Lines.Count ? content.Lines[l] : null;
			Cls(player, viewer, LineIds[l], "hidden", line == null);

			for (var r = 0; r < AdHudLayout.RunsPerLine; r++)
			{
				var run = line != null && r < line.Count ? line[r] : null;
				var id = RunIds[l, r];

				Var(player, viewer, RunVars[l, r], run?.Text ?? "");
				Cls(player, viewer, id, "hidden", run == null);

				// An unused Label keeps its old style: it is collapsed, and the next ad may want it again
				if (run == null)
					continue;

				Cls(player, viewer, id, "gap", run.Gap);
				Cls(player, viewer, id, "b", run.Bold);
				Cls(player, viewer, id, "i", run.Italic);
				Cls(player, viewer, id, "u", run.Underline);
				Cls(player, viewer, id, "kbd", run.Kbd);
				Group(player, viewer, id, "c", run.ColorClass);
			}
		}
	}

	private void Var(CCSPlayerController player, Viewer viewer, string name, string value)
	{
		if (viewer.Vars.TryGetValue(name, out var written) && written == value)
			return;

		_panel!.SetVariableFor(player, name, value);
		viewer.Vars[name] = value;
	}

	private void Cls(CCSPlayerController player, Viewer viewer, string id, string cls, bool on)
	{
		if (viewer.Classes.TryGetValue((id, cls), out var written) && written == on)
			return;

		// Remembered on intent, like the library does: a write that did not land is retried after the next Forget
		_panel!.TrySetClassFor(player, id, cls, on);
		viewer.Classes[(id, cls)] = on;
	}

	/// <summary>At most one class of a group on: the old one off, the new one on, nothing when it is the same.</summary>
	private void Group(CCSPlayerController player, Viewer viewer, string id, string group, string? cls)
	{
		viewer.Groups.TryGetValue((id, group), out var current);
		if (current == cls)
			return;

		if (current != null)
			_panel!.TrySetClassFor(player, id, current, false);

		if (cls != null)
		{
			_panel!.TrySetClassFor(player, id, cls, true);
			viewer.Groups[(id, group)] = cls;
		}
		else
		{
			viewer.Groups.Remove((id, group));
		}
	}
}
