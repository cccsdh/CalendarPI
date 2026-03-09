namespace CalendarPi.Models
{
    public class EventDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string Color { get; set; } = "#3788d8";
        public bool AllDay { get; set; }
    }
}
