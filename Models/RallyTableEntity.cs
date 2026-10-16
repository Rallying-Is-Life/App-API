namespace App_API.Models
{
    public class RallyTableEntity : TableEntityBase
    {
        public int Year { get; set; }
        public string EventName { get; set; }
        public DateTime EventDate { get; set; }
        public string OrganisingClubs { get; set; }
    }
}
