using CalendarPi.Models;
using Ical.Net;
using IcalCalendar = Ical.Net.Calendar;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace CalendarPi.Services
{
    public class CalendarService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly CalendarConfig _config;

        public CalendarService(IHttpClientFactory httpFactory, IOptions<CalendarConfig> config)
        {
            _httpFactory = httpFactory;
            _config = config.Value;
        }

        public async Task<IEnumerable<EventDto>> GetEventsAsync(DateTime start, DateTime end)
        {
            var results = new List<EventDto>();

            foreach (var cal in _config.Items)
            {
                try
                {
                    if (cal.Source?.ToLowerInvariant() == "google" && !string.IsNullOrEmpty(cal.ApiKey))
                    {
                        var evts = await FetchFromGoogleCalendar(cal, start, end);
                        results.AddRange(evts.Select(e => { e.Color = cal.Color; return e; }));
                    }
                    else
                    {
                        var evts = await FetchFromIcs(cal, start, end);
                        results.AddRange(evts.Select(e => { e.Color = cal.Color; return e; }));
                    }
                }
                catch
                {
                    // ignore individual calendar failures
                }
            }

            return results.OrderBy(e => e.Start);
        }

        private async Task<IEnumerable<EventDto>> FetchFromGoogleCalendar(CalendarEntry cal, DateTime start, DateTime end)
        {
            // calendarId must be URL encoded
            var client = _httpFactory.CreateClient();
            var calendarId = Uri.EscapeDataString(cal.Id);
            var timeMin = start.ToString("o");
            var timeMax = end.ToString("o");
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events?key={cal.ApiKey}&timeMin={timeMin}&timeMax={timeMax}&singleEvents=true&orderBy=startTime";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<EventDto>();
            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            var list = new List<EventDto>();
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var title = item.GetProperty("summary").GetString() ?? "";
                    DateTime? sdt = null, edt = null;
                    bool allDay = false;
                    if (item.TryGetProperty("start", out var startElem))
                    {
                        if (startElem.TryGetProperty("dateTime", out var dt))
                        {
                            sdt = DateTime.Parse(dt.GetString()!, null, DateTimeStyles.AdjustToUniversal);
                        }
                        else if (startElem.TryGetProperty("date", out var d))
                        {
                            sdt = DateTime.Parse(d.GetString()!);
                            allDay = true;
                        }
                    }
                    if (item.TryGetProperty("end", out var endElem))
                    {
                        if (endElem.TryGetProperty("dateTime", out var dt))
                        {
                            edt = DateTime.Parse(dt.GetString()!, null, DateTimeStyles.AdjustToUniversal);
                        }
                        else if (endElem.TryGetProperty("date", out var d))
                        {
                            edt = DateTime.Parse(d.GetString()!);
                            allDay = true;
                        }
                    }
                    if (sdt != null)
                    {
                        list.Add(new EventDto { Title = title, Start = sdt.Value, End = edt, AllDay = allDay });
                    }
                }
            }

            return list;
        }

        private async Task<IEnumerable<EventDto>> FetchFromIcs(CalendarEntry cal, DateTime start, DateTime end)
        {
            var client = _httpFactory.CreateClient();
            var url = cal.Id; // expect public ICS URL
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return Enumerable.Empty<EventDto>();
            var text = await resp.Content.ReadAsStringAsync();
            var calendar = IcalCalendar.Load(text);
            var events = new List<EventDto>();
            // Note: for simplicity, expand only explicit events (recurring rules are not expanded here).
            foreach (var e in calendar.Events)
            {
                if (e?.DtStart == null) continue;
                var startDt = e.DtStart.Value;
                var endDt = e.DtEnd?.Value;
                // include events that start within the requested window
                if (startDt >= start && startDt <= end)
                {
                    events.Add(new EventDto
                    {
                        Title = e.Summary ?? "",
                        Start = startDt,
                        End = endDt,
                        AllDay = e.IsAllDay
                    });
                }
            }
            return events;
        }
    }
}
