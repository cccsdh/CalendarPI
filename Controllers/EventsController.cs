using CalendarPi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CalendarPi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly CalendarService _service;
        private readonly IConfiguration _config;

        public EventsController(CalendarService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string start, [FromQuery] string end)
        {
            if (!DateTime.TryParse(start, out var s) || !DateTime.TryParse(end, out var e))
            {
                return BadRequest("start and end query parameters required in YYYY-MM-DD format");
            }

            var events = await _service.GetEventsAsync(s.Date, e.Date.AddDays(1));

            // determine display timezone from configuration (default to Eastern)
            var tzSetting = _config.GetValue<string>("DisplayTimeZone");
            var tz = ResolveTimeZone(tzSetting);

            // return in FullCalendar expected format, converting times to configured timezone

            // Because anonymous-return in LINQ is awkward for the conversion, build result list explicitly
            var outList = new List<object>();
            foreach (var ev in events)
            {
                if (ev.AllDay)
                {
                    outList.Add(new {
                        title = ev.Title,
                        start = ev.Start.ToString("yyyy-MM-dd"),
                        end = ev.End?.ToString("yyyy-MM-dd"),
                        allDay = true,
                        color = ev.Color
                    });
                    continue;
                }

                DateTime ConvertToTarget(DateTime dt)
                {
                    if (dt.Kind == DateTimeKind.Utc)
                    {
                        return TimeZoneInfo.ConvertTimeFromUtc(dt, tz);
                    }
                    else if (dt.Kind == DateTimeKind.Local)
                    {
                        return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.Local, tz);
                    }
                    else // Unspecified - assume UTC
                    {
                        var asUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, tz);
                    }
                }

                var startDt = ConvertToTarget(ev.Start);
                var startDto = new DateTimeOffset(startDt, tz.GetUtcOffset(startDt));
                string startStr = startDto.ToString("o");

                string endStr = null;
                if (ev.End != null)
                {
                    var endDt = ConvertToTarget(ev.End.Value);
                    var endDto = new DateTimeOffset(endDt, tz.GetUtcOffset(endDt));
                    endStr = endDto.ToString("o");
                }

                outList.Add(new {
                    title = ev.Title,
                    start = startStr,
                    end = endStr,
                    allDay = false,
                    color = ev.Color
                });
            }

            return Ok(outList);
        }

        private static TimeZoneInfo ResolveTimeZone(string? tzSetting)
        {
            if (string.IsNullOrEmpty(tzSetting) || tzSetting.Equals("Eastern", System.StringComparison.OrdinalIgnoreCase))
            {
                // Map 'Eastern' to platform-specific timezone id
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    tzSetting = "Eastern Standard Time";
                }
                else
                {
                    tzSetting = "America/New_York";
                }
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzSetting);
            }
            catch
            {
                // fallback to local timezone
                return TimeZoneInfo.Local;
            }
        }
    }
}
