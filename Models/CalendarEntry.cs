namespace CalendarPi.Models
{
    public class CalendarEntry
    {
        public string Id { get; set; } = string.Empty; // calendarId or ICS URL
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#3788d8";
        public string Source { get; set; } = "ics"; // "ics" or "google"
        public string? ApiKey { get; set; }
    }
}
