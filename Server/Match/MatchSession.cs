namespace CG.Match;

public class MatchSession
{
    public Guid Id;
    public string WhiteUsername { get; set; }
    public string BlackUsername { get; set; }
    public int Stake { get; set; }
}