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
  "ServerId": 1
}
```

## Credits

- **[partiusfabaa](https://github.com/partiusfabaa):** For his base original plugin.
