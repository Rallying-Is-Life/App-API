using Azure;
using Azure.Data.Tables;
namespace App_API.Models;
public class ClubProfile
{
    public string ClubName { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public List<RallyLink> Rallies { get; set; } = new();
}
public class RallyLink
{
    public string RallyName { get; set; } = string.Empty;
    public string RallyRowKey { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime EventDate { get; set; }
}
public class ClubRallyIndexEntity : TableEntityBase
{
    public string RallyName { get; set; } = string.Empty;
    public string RallyRowKey { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
}
