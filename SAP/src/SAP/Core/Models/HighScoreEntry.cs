using System;

namespace SAP.Core.Models;

public class HighScoreEntry
{
    public string Name { get; set; } = "PLAYER";
    public int Score { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
