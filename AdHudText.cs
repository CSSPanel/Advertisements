using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Advertisements.Hud;

/// <summary>
/// The look of one HUD ad: the `hud` JSON column of cssp_advertisements (ads-hud.sql in the website repo).
///
///   {"position":"chat|top","accent":"orange|gold|red|blue|green|purple|plain",
///    "icon":"info|star|trophy|news|globe|warning"|null,"title":"..."|null,"duration":3-15,"html":false}
///
/// <para>Read the way the website reads it (packages/config/utils/cs2/advertisements.ts): a missing key takes
/// DEFAULT_AD_HUD_STYLE, an unknown accent is orange, an unknown icon is no icon, any position but "top" is the
/// chat one. A row that is not JSON at all gets every default, so the ad still shows.</para>
/// </summary>
public sealed class AdHudStyle
{
	public string Position { get; private init; } = "chat";
	public string Accent { get; private init; } = "orange";

	/// <summary>The website's icon name (info, star...), or null for none.</summary>
	public string? Icon { get; private init; } = "info";

	public string? Title { get; private init; }
	public int Duration { get; private init; } = AdHudFormat.DurationDefault;
	public bool Html { get; private init; }

	public static AdHudStyle Default { get; } = new();

	/// <summary>Parses the column. <paramref name="error"/> is set when it was there but unreadable.</summary>
	public static AdHudStyle Parse(string? json, out string? error)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(json))
			return Default;

		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// A driver that stored the JSON as a JSON string: parse what is inside
			if (root.ValueKind == JsonValueKind.String)
				return Parse(root.GetString(), out error);

			if (root.ValueKind != JsonValueKind.Object)
			{
				error = $"not an object ({root.ValueKind})";
				return Default;
			}

			var position = Str(root, "position") == "top" ? "top" : "chat";

			var accent = Str(root, "accent") ?? "orange";
			if (!AdHudFormat.Accents.ContainsKey(accent))
				accent = "orange";

			// A missing key is the default icon; an explicit null is none
			string? icon = "info";
			if (root.TryGetProperty("icon", out var iconEl))
				icon = iconEl.ValueKind == JsonValueKind.String && AdHudFormat.Icons.ContainsKey(iconEl.GetString()!) ? iconEl.GetString() : null;

			var title = Str(root, "title")?.Trim();
			if (string.IsNullOrEmpty(title))
				title = null;

			var duration = AdHudFormat.DurationDefault;
			if (root.TryGetProperty("duration", out var durEl) && durEl.ValueKind == JsonValueKind.Number && durEl.TryGetDouble(out var d))
				duration = (int)Math.Round(d);
			duration = Math.Clamp(duration, AdHudFormat.DurationMin, AdHudFormat.DurationMax);

			var html = root.TryGetProperty("html", out var htmlEl) && htmlEl.ValueKind == JsonValueKind.True;

			return new AdHudStyle
			{
				Position = position,
				Accent = accent,
				Icon = icon,
				Title = title,
				Duration = duration,
				Html = html,
			};
		}
		catch (JsonException ex)
		{
			error = ex.Message;
			return Default;
		}
	}

	private static string? Str(JsonElement root, string name) =>
		root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}

/// <summary>
/// A port of the website's converter (packages/config/utils/cs2/advertisements.ts, adHudHtml and
/// sanitizeAdHudHtml), so the strip says what the admin preview says. Keep the two in step: a change to the
/// formats there is a change here.
///
///   Formats mode   {Color} tags and {VARIABLES} as in chat ads, plus {{b:text}}, {{cmd:!vip}} (the accent
///                  colour, bold), {{accent:text}} and {{br}}
///   HTML mode      only b, i, u, br and font color="#hex" are kept; {VARIABLES} are still replaced
///
/// <para>The website's result is HTML, but a custom_hud_layout Label takes no markup (the html attribute is
/// not on the CustomHud whitelist). So the HTML is read back into styled pieces here, the way the admin
/// preview's renderLabel reads it, and <see cref="AdHudLayout"/> lays the pieces out over a pool of Labels.</para>
/// </summary>
public static class AdHudFormat
{
	public const int DurationMin = 3;
	public const int DurationMax = 15;
	public const int DurationDefault = 7;

	/// <summary>AD_HUD_ACCENTS. The layout has one ac-name class per entry.</summary>
	public static readonly IReadOnlyDictionary<string, string> Accents = new Dictionary<string, string>
	{
		["orange"] = "#f7783c",
		["gold"] = "#f0a531",
		["red"] = "#e0432f",
		["blue"] = "#56a0dd",
		["green"] = "#4caf6d",
		["purple"] = "#9b6cd6",
		["plain"] = "#8b939c",
	};

	/// <summary>AD_HUD_ICONS: the website's name to CS2's icons/ui file. The layout has one ic-name class per entry.</summary>
	public static readonly IReadOnlyDictionary<string, string> Icons = new Dictionary<string, string>
	{
		["info"] = "info",
		["star"] = "favorite_star_filled",
		["trophy"] = "trophy",
		["news"] = "news",
		["globe"] = "globe",
		["warning"] = "warning",
	};

	/// <summary>AD_CHAT_COLORS: the chat colour tags and the colour each one prints in game.</summary>
	public static readonly IReadOnlyDictionary<string, string> ChatColors = new Dictionary<string, string>
	{
		["DEFAULT"] = "#fdfdfd",
		["White"] = "#fefefe",
		["DarkRed"] = "#f30000",
		["Green"] = "#40ff3f",
		["LightYellow"] = "#dbd46c",
		["LightBlue"] = "#5e98d8",
		["Olive"] = "#a5a500",
		["Lime"] = "#a4fd49",
		["Red"] = "#fa4041",
		["LightPurple"] = "#b681ed",
		["Purple"] = "#d02fe1",
		["Grey"] = "#c4c9cf",
		["Yellow"] = "#eee27e",
		["Gold"] = "#d5a332",
		["Silver"] = "#afc1d5",
		["Blue"] = "#5c98d7",
		["DarkBlue"] = "#4b69fd",
		["BlueGrey"] = "#b0c2d6",
		["Magenta"] = "#f000f0",
		["LightRed"] = "#ea4c4d",
		["Orange"] = "#f5a623",
	};

	private static readonly Regex Variable = new(@"\{([A-Z]+)\}", RegexOptions.CultureInvariant);
	private static readonly Regex Br = new(@"\{\{br\}\}", RegexOptions.CultureInvariant);
	private static readonly Regex Bold = new(@"\{\{b:([^}]*)\}\}", RegexOptions.CultureInvariant);
	private static readonly Regex Cmd = new(@"\{\{cmd:([^}]*)\}\}", RegexOptions.CultureInvariant);
	private static readonly Regex AccentText = new(@"\{\{accent:([^}]*)\}\}", RegexOptions.CultureInvariant);
	private static readonly Regex ColorTag = new(@"\{(" + string.Join("|", ChatColors.Keys) + @")\}", RegexOptions.CultureInvariant);
	private static readonly Regex AnyTag = new(@"<\/?([a-z]+)([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex FontColor = new(@"color\s*=\s*[""']?(#[0-9a-f]{6})[""']?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static string EscapeHtml(string text) =>
		text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

	public static string UnescapeHtml(string text) =>
		text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");

	/// <summary>sanitizeAdHudHtml: keeps only the tags Panorama's label would take. A filter, not a parser.</summary>
	public static string Sanitize(string html) =>
		AnyTag.Replace(html, m =>
		{
			var name = m.Groups[1].Value.ToLowerInvariant();
			var closing = m.Value.StartsWith("</", StringComparison.Ordinal);

			switch (name)
			{
				case "b":
				case "i":
				case "u":
					return closing ? $"</{name}>" : $"<{name}>";
				case "br":
					return closing ? "" : "<br>";
				case "font":
					if (closing)
						return "</font>";
					var color = FontColor.Match(m.Groups[2].Value);
					return color.Success ? $"<font color=\"{color.Groups[1].Value}\">" : "<font>";
				default:
					return "";
			}
		});

	/// <summary>
	/// adHudHtml: the strip's text as the website's HTML. <paramref name="variables"/> fill {NAME} and the rest;
	/// an unknown one is left as written.
	/// </summary>
	public static string ToHtml(string text, string accentName, bool html, IReadOnlyDictionary<string, string> variables)
	{
		var accent = Accents.TryGetValue(accentName, out var a) ? a : Accents["orange"];
		var withVars = Variable.Replace(text, m => variables.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

		if (html)
			return Sanitize(withVars);

		var output = EscapeHtml(withVars);

		// {{formats}} take plain text only: a } ends them, so a {Color} tag cannot sit inside one
		output = Br.Replace(output, "<br>");
		output = Bold.Replace(output, m => $"<b>{m.Groups[1].Value}</b>");
		output = Cmd.Replace(output, m => $"<font color=\"{accent}\"><b>{m.Groups[1].Value}</b></font>");
		output = AccentText.Replace(output, m => $"<font color=\"{accent}\">{m.Groups[1].Value}</font>");

		// {Color} tags: each one colours the text up to the next tag
		var open = false;
		output = ColorTag.Replace(output, m =>
		{
			var name = m.Groups[1].Value;
			var close = open ? "</font>" : "";
			open = name != "DEFAULT";
			return open ? $"{close}<font color=\"{ChatColors[name]}\">" : close;
		});
		if (open)
			output += "</font>";

		return output;
	}
}

/// <summary>How one piece of the strip's text is drawn.</summary>
/// <param name="Color">"#rrggbb" from a font tag, or null for the strip's text colour.</param>
public readonly record struct AdHudPieceStyle(string? Color, bool Bold, bool Italic, bool Underline);

/// <summary>A piece of text in one style, or a line break.</summary>
public readonly record struct AdHudPiece(string Text, AdHudPieceStyle Style, bool Break)
{
	public static AdHudPiece LineBreak { get; } = new("", default, true);
}

/// <summary>One Label on the strip.</summary>
/// <param name="ColorClass">The c-rrggbb class from the layout's palette, or null for the strip's text colour.</param>
/// <param name="Gap">A word space to its left, as a margin: a Label trims the spaces at its own edges.</param>
public sealed record AdHudRun(string Text, string? ColorClass, bool Bold, bool Italic, bool Underline, bool Gap);

/// <summary>Everything one player's strip shows for one ad.</summary>
public sealed class AdHudContent
{
	public required string Position { get; init; }
	public required string Accent { get; init; }

	/// <summary>The ic- name (the website's icon name), or null for none.</summary>
	public required string? Icon { get; init; }

	/// <summary>Empty for the one-line strip; a title makes the taller one.</summary>
	public required string Title { get; init; }

	public required int Duration { get; init; }

	/// <summary>The text reads right to left: icon and edge on the right, lines right-aligned.</summary>
	public required bool Rtl { get; init; }

	/// <summary>Lines of runs, already in the left-to-right order they are drawn in.</summary>
	public required IReadOnlyList<IReadOnlyList<AdHudRun>> Lines { get; init; }

	/// <summary>The text with every format taken off, for chat when the HUD can't show it.</summary>
	public required string PlainText { get; init; }
}

/// <summary>
/// Turns an ad into <see cref="AdHudContent"/>: the website's HTML read back into styled pieces, then broken
/// into lines over the layout's pool of Labels (<see cref="Lines"/> x <see cref="RunsPerLine"/>).
///
/// <para><b>Lines are broken here, not by the client.</b> Panels never wrap, and the strip is a row of Labels
/// per line (a Label has no spans), so the plugin has to decide where a line ends. It measures with a per-glyph
/// estimate of Stratum2 at 13.5px, on the generous side: a wrong guess makes a line end a word early, and a line
/// that is still too long is squished a little by the client rather than cut.</para>
/// </summary>
public static class AdHudLayout
{
	/// <summary>Line panels in ads_hud.xml.</summary>
	public const int Lines = 4;

	/// <summary>Labels per line in ads_hud.xml.</summary>
	public const int RunsPerLine = 8;

	/// <summary>The strip's max-width is 500. Take off the edge, the padding, the icon and its gap, and a margin.</summary>
	private const float StripWidth = 500f;
	private const float Chrome = 2f + 10f + 14f + 10f;
	private const float IconSmall = 15f + 9f;
	private const float IconBig = 20f + 9f;

	private const float FontPx = 13.5f;
	private const float BoldFactor = 1.06f;

	/// <summary>
	/// The colours the layout has a c- class for: the 7 accents, the chat colours, white for bold, the strip's
	/// own text colour, and two common HTML ones. A font colour outside the list takes the nearest.
	/// </summary>
	public static readonly string[] Palette =
	[
		"f7783c", "f0a531", "e0432f", "56a0dd", "4caf6d", "9b6cd6", "8b939c",
		"f30000", "40ff3f", "dbd46c", "5e98d8", "a5a500", "a4fd49", "fa4041", "b681ed", "d02fe1", "c4c9cf",
		"eee27e", "d5a332", "afc1d5", "5c98d7", "4b69fd", "b0c2d6", "f000f0", "ea4c4d", "f5a623",
		"ffffff", "d7dce1", "00ffff", "ff69b4",
	];

	private static readonly Regex Tokens = new(@"<(\/?)(b|i|u|br|font)(?:\s+color=""(#[0-9a-f]{6})"")?>",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static AdHudContent Build(string text, AdHudStyle style, IReadOnlyDictionary<string, string> variables)
	{
		var html = AdHudFormat.ToHtml(text, style.Accent, style.Html, variables);
		var pieces = Parse(html);

		// The title is plain text on the website too; only {VARIABLES} are filled in
		var title = style.Title == null ? "" : ReplaceVariables(style.Title, variables).Trim();

		var rtl = FirstStrongIsRtl(title.Length > 0 ? [new AdHudPiece(title, default, false), .. pieces] : pieces);

		var budget = StripWidth - Chrome - (style.Icon == null ? 0f : title.Length > 0 ? IconBig : IconSmall);
		var lines = Wrap(pieces, budget);

		if (rtl)
			lines = lines.Select(Mirror).ToList();

		return new AdHudContent
		{
			Position = style.Position,
			Accent = style.Accent,
			Icon = style.Icon,
			Title = title,
			Duration = style.Duration,
			Rtl = rtl,
			Lines = lines,
			PlainText = Plain(pieces),
		};
	}

	private static readonly Regex Variable = new(@"\{([A-Z]+)\}", RegexOptions.CultureInvariant);

	private static string ReplaceVariables(string text, IReadOnlyDictionary<string, string> variables) =>
		Variable.Replace(text, m => variables.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

	// ------------------------------------------------------------------ HTML to pieces

	/// <summary>
	/// The admin preview's renderLabel: b, i, u, br and font color, and nothing else. A closing tag closes the
	/// nearest open tag of its name and everything opened inside it; tags left open still style their text.
	/// A newline in the text is a line break too, as the preview draws it (pre-wrap).
	/// </summary>
	public static List<AdHudPiece> Parse(string html)
	{
		var pieces = new List<AdHudPiece>();
		var stack = new List<(string Tag, string? Color)>();
		var last = 0;

		void Emit(string raw)
		{
			if (raw.Length == 0)
				return;

			var style = StyleOf(stack);
			var parts = AdHudFormat.UnescapeHtml(raw).Replace("\r", "").Split('\n');

			for (var i = 0; i < parts.Length; i++)
			{
				if (i > 0)
					pieces.Add(AdHudPiece.LineBreak);
				if (parts[i].Length > 0)
					pieces.Add(new AdHudPiece(parts[i], style, false));
			}
		}

		foreach (Match m in Tokens.Matches(html))
		{
			Emit(html[last..m.Index]);
			last = m.Index + m.Length;

			var closing = m.Groups[1].Value.Length > 0;
			var tag = m.Groups[2].Value.ToLowerInvariant();

			if (tag == "br")
			{
				if (!closing)
					pieces.Add(AdHudPiece.LineBreak);
				continue;
			}

			if (!closing)
			{
				stack.Add((tag, m.Groups[3].Success ? m.Groups[3].Value : null));
				continue;
			}

			var at = stack.FindLastIndex(s => s.Tag == tag);
			if (at >= 0)
				stack.RemoveRange(at, stack.Count - at);
		}

		Emit(html[last..]);
		return pieces;
	}

	private static AdHudPieceStyle StyleOf(List<(string Tag, string? Color)> stack)
	{
		string? color = null;
		bool bold = false, italic = false, underline = false;

		foreach (var (tag, c) in stack)
		{
			switch (tag)
			{
				case "b": bold = true; break;
				case "i": italic = true; break;
				case "u": underline = true; break;
				// The innermost font with a colour wins; a bare <font> inherits
				case "font" when c != null: color = c; break;
			}
		}

		return new AdHudPieceStyle(color, bold, italic, underline);
	}

	private static string Plain(List<AdHudPiece> pieces)
	{
		var sb = new StringBuilder();
		foreach (var piece in pieces)
			sb.Append(piece.Break ? " " : piece.Text);

		return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
	}

	// ------------------------------------------------------------------ direction

	private static bool IsRtl(char c) =>
		c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿';

	/// <summary>
	/// The first letter decides, so "{Red}שלום" is right to left. The website's preview only tests the first
	/// character of the raw text; in the strip a Hebrew line laid out left to right would read backwards,
	/// because every coloured piece is its own Label.
	/// </summary>
	private static bool FirstStrongIsRtl(IEnumerable<AdHudPiece> pieces)
	{
		foreach (var piece in pieces)
		{
			foreach (var c in piece.Text)
			{
				if (IsRtl(c))
					return true;
				if (char.IsLetter(c))
					return false;
			}
		}

		return false;
	}

	/// <summary>
	/// A right-to-left line is drawn as its runs in reverse, flowing right (and right-aligned by the layout).
	/// The gap that sat before run k now sits to the left of the run drawn before it.
	/// </summary>
	private static IReadOnlyList<AdHudRun> Mirror(IReadOnlyList<AdHudRun> line)
	{
		var n = line.Count;
		var mirrored = new List<AdHudRun>(n);

		for (var j = 0; j < n; j++)
		{
			var run = line[n - 1 - j];
			var gap = j > 0 && line[n - j].Gap;
			mirrored.Add(run with { Gap = gap });
		}

		return mirrored;
	}

	// ------------------------------------------------------------------ lines

	/// <summary>One glyph's advance in em, for Stratum2 Regular, rounded up.</summary>
	private static float Em(char c) => c switch
	{
		' ' => 0.26f,
		'i' or 'l' or 'j' or '!' or '|' or '.' or ',' or ':' or ';' or '\'' or '`' => 0.26f,
		'f' or 'r' or 't' or 'I' or '(' or ')' or '[' or ']' or '{' or '}' or '"' or '-' or '/' or '\\' or '*' => 0.37f,
		'm' or 'w' => 0.80f,
		'M' or 'W' or '@' or '%' or '&' => 0.88f,
		>= 'A' and <= 'Z' => 0.63f,
		>= '0' and <= '9' => 0.56f,
		>= 'a' and <= 'z' => 0.53f,
		>= '֐' and <= '׿' => 0.57f,
		>= '؀' and <= 'ࣿ' => 0.52f,
		_ when char.IsSurrogate(c) => 0.55f,
		>= '⺀' => 1.0f,
		_ => 0.62f,
	};

	private static float Width(string text, bool bold)
	{
		var em = 0f;
		foreach (var c in text)
			em += Em(c);

		return em * FontPx * (bold ? BoldFactor : 1f);
	}

	private static readonly float SpaceWidth = Em(' ') * FontPx;

	private sealed class Seg
	{
		public required StringBuilder Text;
		public required AdHudPieceStyle Style;
		public required bool Gap;
	}

	/// <summary>Breaks the pieces into lines no wider than <paramref name="budget"/>, at spaces where it can.</summary>
	private static List<IReadOnlyList<AdHudRun>> Wrap(List<AdHudPiece> pieces, float budget)
	{
		var lines = new List<List<Seg>> { new() };
		var lineWidth = 0f;
		var space = false;

		void NewLine()
		{
			lines.Add([]);
			lineWidth = 0f;
			space = false;
		}

		void Append(string word, AdHudPieceStyle style, bool gap, float width)
		{
			var line = lines[^1];
			var prev = line.Count > 0 ? line[^1] : null;

			if (prev != null && prev.Style == style)
			{
				if (gap)
					prev.Text.Append(' ');
				prev.Text.Append(word);
			}
			else
			{
				line.Add(new Seg { Text = new StringBuilder(word), Style = style, Gap = gap });
			}

			lineWidth += width;
		}

		foreach (var piece in pieces)
		{
			if (piece.Break)
			{
				NewLine();
				continue;
			}

			var text = piece.Text;
			var i = 0;

			while (i < text.Length)
			{
				if (char.IsWhiteSpace(text[i]))
				{
					space = true;
					i++;
					continue;
				}

				var end = i;
				while (end < text.Length && !char.IsWhiteSpace(text[end]))
					end++;

				var word = text[i..end];
				i = end;

				var bold = piece.Style.Bold;
				var w = Width(word, bold);
				var gap = space && lines[^1].Count > 0;
				space = false;

				if (lines[^1].Count > 0 && lineWidth + (gap ? SpaceWidth : 0f) + w > budget)
				{
					NewLine();
					gap = false;
				}

				// A word longer than a whole line is cut where it runs out of room
				while (w > budget - lineWidth - (gap ? SpaceWidth : 0f) && word.Length > 1)
				{
					var room = budget - lineWidth - (gap ? SpaceWidth : 0f);
					var take = 0;
					var used = 0f;

					while (take < word.Length - 1)
					{
						var cw = Em(word[take]) * FontPx * (bold ? BoldFactor : 1f);
						if (used + cw > room && take > 0)
							break;
						used += cw;
						take++;
					}

					Append(word[..take], piece.Style, gap, (gap ? SpaceWidth : 0f) + used);
					NewLine();
					gap = false;
					word = word[take..];
					w = Width(word, bold);
				}

				Append(word, piece.Style, gap, (gap ? SpaceWidth : 0f) + w);
			}
		}

		// Blank lines at either end show nothing but height
		while (lines.Count > 1 && lines[^1].Count == 0)
			lines.RemoveAt(lines.Count - 1);
		while (lines.Count > 1 && lines[0].Count == 0)
			lines.RemoveAt(0);

		// More lines than the pool: the last one shown ends in "..."
		if (lines.Count > Lines)
		{
			lines.RemoveRange(Lines, lines.Count - Lines);
			var last = lines[^1];
			if (last.Count == 0)
				last.Add(new Seg { Text = new StringBuilder("..."), Style = default, Gap = false });
			else
				last[^1].Text.Append("...");
		}

		return lines.Select(line => (IReadOnlyList<AdHudRun>)Fit(line)).ToList();
	}

	/// <summary>A line with more styles than Labels: the last Label takes the rest, in its own style.</summary>
	private static List<AdHudRun> Fit(List<Seg> line)
	{
		var runs = new List<AdHudRun>();

		for (var k = 0; k < line.Count; k++)
		{
			var seg = line[k];

			if (runs.Count == RunsPerLine - 1 && k < line.Count - 1)
			{
				var rest = new StringBuilder(seg.Text.ToString());
				for (var m = k + 1; m < line.Count; m++)
				{
					if (line[m].Gap)
						rest.Append(' ');
					rest.Append(line[m].Text);
				}

				runs.Add(Run(rest.ToString(), seg.Style, seg.Gap));
				break;
			}

			runs.Add(Run(seg.Text.ToString(), seg.Style, seg.Gap));
		}

		return runs;
	}

	private static AdHudRun Run(string text, AdHudPieceStyle style, bool gap)
	{
		// A font colour wins; bold text without one is white, the rest takes the strip's own colour
		var color = style.Color != null ? Nearest(style.Color) : style.Bold ? "ffffff" : null;
		return new AdHudRun(text, color == null ? null : "c-" + color, style.Bold, style.Italic, style.Underline, gap);
	}

	/// <summary>The palette entry closest to a #rrggbb colour (redmean distance).</summary>
	public static string Nearest(string hex)
	{
		var wanted = hex.TrimStart('#').ToLowerInvariant();
		if (Palette.Contains(wanted))
			return wanted;

		if (wanted.Length != 6 || !int.TryParse(wanted, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
			return "d7dce1";

		int r = (rgb >> 16) & 0xff, g = (rgb >> 8) & 0xff, b = rgb & 0xff;
		var best = Palette[0];
		var bestDistance = double.MaxValue;

		foreach (var entry in Palette)
		{
			var e = Convert.ToInt32(entry, 16);
			int er = (e >> 16) & 0xff, eg = (e >> 8) & 0xff, eb = e & 0xff;
			var mean = (r + er) / 2.0;
			int dr = r - er, dg = g - eg, db = b - eb;
			var distance = (2 + mean / 256) * dr * dr + 4 * dg * dg + (2 + (255 - mean) / 256) * db * db;

			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = entry;
			}
		}

		return best;
	}
}
