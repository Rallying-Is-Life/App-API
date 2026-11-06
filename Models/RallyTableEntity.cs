namespace App_API.Models
{
    public class RallyTableEntity : TableEntityBase
{
        public int Year { get; set; }
        public string EventName { get; set; }
        public DateTime EventDate { get; set; }
        public string OrganisingClubs { get; set; }
        public double CompetitiveDistanceKm { get; set; }
        public string DistanceUnit { get; set; }
        public string Website { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? FlagCode { get; set; }
        public string? LogoUrl { get; set; }
    }
}