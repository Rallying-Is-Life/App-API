using System.ComponentModel;

namespace App_API.Models;

public class RallyProfile : IEquatable<RallyProfile>
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
    public double Length { get; set; }
    public int StageCount { get; set; }

    public bool Equals(RallyProfile other) =>
        Name == other.Name &&
        Date == other.Date &&
        Length == other.Length &&
        StageCount == other.StageCount;

    public override bool Equals(object? obj) => obj is RallyProfile other && Equals(other);

    public override int GetHashCode() => Name.GetHashCode() ^ Date.GetHashCode() ^ Length.GetHashCode() ^ StageCount.GetHashCode();
}
