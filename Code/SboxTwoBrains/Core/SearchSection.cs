namespace SboxTwoBrains.Core;

/// <summary>Systematic + stimulus-driven search behaviour.</summary>
public sealed class SearchSection
{
	/// <summary>Window in which a searched/sensed position still qualifies systematic search. Range: [1, 600].</summary>
	public double? SystematicWindowSeconds { get; set; }
	/// <summary>A node searched within this window is skipped. Range: [0, 600].</summary>
	public double? NodeRevisitPenaltySeconds { get; set; }
	/// <summary>Give up an unproductive search after this long. Range: [1, 3600].</summary>
	public double? GiveUpSeconds { get; set; }
	/// <summary>Nodes visited per search episode before resting. Range: [1, 64].</summary>
	public int? MaxNodesPerSearch { get; set; }
	/// <summary>Distance at which a searched point counts as reached (metres). Range: [0.1, 100].</summary>
	public double? NodeReachDistance { get; set; }
}
