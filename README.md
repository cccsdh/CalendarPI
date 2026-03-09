CalendarPi - Raspberry Pi Calendar Kiosk

[![Build and Release](https://github.com/cccsdh/CalendarPI/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/cccsdh/CalendarPI/actions/workflows/build-and-release.yml)

This is a simple ASP.NET Core web application that displays a month calendar and pulls public events from Google Calendars/ICS feeds configured in `appsettings.json`.

Features
- Month view calendar (FullCalendar)
- Events pulled from public ICS URLs or Google Calendar API (public calendars)
- Events color-coded per calendar
- Previous/Next month navigation

Running in Visual Studio
1. Open the `CalendarPi` folder as a project in Visual Studio.
2. Restore NuGet packages and build.
3. Run the project (F5). The app will open in the browser and show the calendar. By default it uses the sample public ICS in `appsettings.json`.

Configuration
- `appsettings.json` contains a `Calendars` section with an `Items` array.
- Each calendar entry supports:
  - `Id`: For `ics` source this should be a public ICS URL. For `google` source this should be the calendar ID (e.g. `en.usa%23holiday@group.v.calendar.google.com`).
  - `Name`: Friendly name (not currently displayed but saved for future use)
  - `Color`: Hex color string for the calendar events
  - `Source`: `ics` or `google`
  - `ApiKey`: Required only for `google` source (a public API key with Calendar API enabled)

Display time settings
- `DisplayTimeZone`: controls how event times are presented in the UI. By default the project uses `"Eastern"` which maps to the platform time zone for US Eastern time (handles EST/EDT automatically).

Examples you can put in `appsettings.json`:

```json
{
  "DisplayTimeZone": "Eastern"            // convenience alias -> Windows: "Eastern Standard Time", Linux/macOS: "America/New_York"
}
```

You may also set a specific system time zone id:
- Windows example: `"Eastern Standard Time"`
- Linux/macOS (IANA) example: `"America/New_York"`

Notes:
- The server converts event times to the configured timezone before returning them to the client; DST (EST/EDT) is handled by the system time zone.
- If an invalid id is supplied the app falls back to the server's local timezone.

Example:
{
  "Calendars": {
    "Items": [
      {
        "Id": "https://calendar.google.com/calendar/ical/en.usa%23holiday%40group.v.calendar.google.com/public/basic.ics",
        "Name": "US Holidays",
        "Color": "#d9534f",
        "Source": "ics"
      }
    ]
  }
}

Deploying to Raspberry Pi
1. Install .NET runtime for ARM on your Raspberry Pi (install .NET 8 runtime for ARM64 or ARM32 depending on your Pi OS). See https://dotnet.microsoft.com/en-us/download/dotnet
2. Publish the app from Visual Studio or CLI. Example targeting .NET 8 and ARM64:
   - `dotnet publish -c Release -r linux-arm64 --self-contained false /p:PublishTrimmed=false /p:TargetFramework=net8.0`
3. Copy the published output to the Pi (scp, rsync, USB, etc.).
4. On the Pi, run the app: `./CalendarPi` (or `dotnet CalendarPi.dll`).
5. To make it start on boot, create a `systemd` service unit, e.g. `/etc/systemd/system/calendarpi.service`:

[Unit]
Description=CalendarPi Kiosk
After=network.target

[Service]
WorkingDirectory=/path/to/app
ExecStart=/usr/bin/dotnet /path/to/app/CalendarPi.dll
Restart=always
# Optional: run as a less-privileged user
User=www-data
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target

6. Enable and start the service:
- `sudo systemctl enable calendarpi`
- `sudo systemctl start calendarpi`

Optionally, place the app behind nginx as a reverse proxy or configure the Pi to open a browser in kiosk mode pointed at the Pi's address.

License
MIT
