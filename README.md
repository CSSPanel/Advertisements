# CSSPanel Advertisements Database Plugin

## Description

An advertisements plugin that works with the CSSPanel as an internal module, allows you to create and sync your own customized advertisements with the CSSPanel. The plugin uses a MySQL database to store and retrieve the advertisements, which can be displayed at regular intervals on the server.

## Requirements

To use this plugin, you need to have the following requirements installed:

- [CounterStrikeSharp](https://docs.cssharp.dev/guides/getting-started/)
- [CSSPanel](https://github.com/CSSPanel/Panel)

## Installation & Usage

Follow these steps to install the plugin on your servers:

1. **Download the latest release:**

   - Download the [latest release](https://github.com/CSSPanel/Advertisements/releases)

2. **Extract the Downloaded Files:**

   - Extract the downloaded plugin under "/game/csgo/addons/counterstrikesharp/plugins/Advertisements"

3. **Start & edit the config file:**

   - Start your server and a new configuration file will be generated under "/game/csgo/addons/counterstrikesharp/configs/plugins/Advertisements/Advertisements.json"
   - Edit the configuration file to customize the plugin settings according to your server, and set the db connection details.

4. **Restart the Server:**

   - Restart your CSS server or change the map to apply the changes.
   - The plugin should now be installed and ready for use.

5. **Usage:**
   - Head over to your panel.
   - Go to the Advertisements section.
   - Create a new advertisement.
   - The advertisement will be displayed on the server at regular intervals.
   - You can also reload the advertisements using the reload button in the panel.

## Configuration

The plugin automatically generates a configuration file in the same location as the plugin DLL. You can edit this configuration file to customize the plugin settings according to your server's needs.

Example Configuration (`/game/csgo/addons/counterstrikesharp/configs/plugins/Advertisements/Advertisements.json`):

```json
{
  "ChatPrefix": "[CSS]",
  "DatabaseHost": "",
  "DatabasePort": 3306,
  "DatabaseUser": "root",
  "DatabasePassword": "",
  "DatabaseName": "",
  "Timer": "25",
  "ServerId": 0
}
```

### Server id

Ads can be targeted at servers and server groups, by the server's id in `sa_servers`. The plugin asks
[CS2-SimpleAdmin](https://github.com/CSSPanel/Plugin) for it - SimpleAdmin finds (or creates) that row by the
server's address - so nothing has to be set per server. Until SimpleAdmin knows the id (a second or two after
start) only the ads for every server are shown.

Ads can also be targeted at nodes: the server's node is that same row's `node_id`, set in the panel's server
settings.

`ServerId` in the config is used only on a server without CS2-SimpleAdmin. The `css_serverid` and `css_node`
convars are gone.

### HUD ads

Ads with location `hud` show as a strip over the chat or under the score, for 3-15 seconds, drawn through
Panorama ([PanoramaManager](https://github.com/Next-il/PanoramaManager)). Their look (position, accent color,
icon, title, time on screen, HTML mode) is the `hud` JSON column the panel writes, and the text takes the chat
ads' `{Color}` tags and `{VARIABLES}` plus `{{b:text}}`, `{{cmd:!vip}}`, `{{accent:text}}` and `{{br}}`.

The layout ships in the workshop addon: `hud/panorama/layout/custom_game/ads_hud.xml` with
`hud/panorama/styles/custom_game/ads_hud.css` and `hud_shared.css`. The server needs CounterStrikeSharp 1.0.374
or newer.

```json
"Hud": {
  "Enabled": true,
  "WaitForFreezeEnd": true,
  "IntervalSeconds": 20
}
```

- `Enabled` - false prints HUD ads in chat instead (so does a strip that failed to start).
- `WaitForFreezeEnd` - a HUD ad that comes up in freeze time waits until the round goes live.
- `IntervalSeconds` - seconds between HUD ads. They have their own rotation and timer, next to `Timer` for chat
  and center ads, so a chat line and a strip can come at the same moment. A tick that finds the last strip still
  on screen is skipped. With the strip off, HUD ads print in chat on the `Timer` rotation instead.

## Credits

- **[partiusfabaa](https://github.com/partiusfabaa):** For his base original plugin.
