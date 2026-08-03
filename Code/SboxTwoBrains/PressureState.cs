using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Complete serializable macro state (see clean-room spec §Macro state). All timers in
/// seconds remaining; counts non-negative; exactly one mode active at a time.
/// </summary>
public sealed class PressureState
{
	public PressureMode Mode { get; set; } = PressureMode.Normal;

	/// <summary>Gauge progression in [0,1].</summary>
	public double Progression { get; set; }

	/// <summary>Completed opportunities this session. &gt;= 0.</summary>
	public int CompletedOpportunities { get; set; }

	/// <summary>Progress toward the event quota. &gt;= 0.</summary>
	public int EventQuotaProgress { get; set; }

	/// <summary>Randomized event quota target; 0 = quota disabled/exhausted.</summary>
	public int EventQuotaTarget { get; set; }

	public bool Enabled { get; set; } = true;

	/// <summary>A viable candidate region is currently held.</summary>
	public bool CandidateLatched { get; set; }

	/// <summary>Seconds of post-completion cooldown remaining. &gt;= 0.</summary>
	public double CooldownRemaining { get; set; }

	/// <summary>Seconds of decrease grace remaining. &gt;= 0.</summary>
	public double DecreaseDelayRemaining { get; set; }

	/// <summary>Currently latched candidate region id; empty when none.</summary>
	public string ActiveCandidateId { get; set; } = "";

	/// <summary>Outstanding opportunity id the host may still ack; empty when none.</summary>
	public string PendingOpportunityId { get; set; } = "";

	/// <summary>Tick the pending opportunity lapses.</summary>
	public long OpportunityExpiryTick { get; set; }

	/// <summary>True once the pending opportunity consumed its single defer extension.</summary>
	public bool PendingDeferExtensionUsed { get; set; }

	/// <summary>Tick of the last mode transition (-1 = never).</summary>
	public long LastTransitionTick { get; set; } = -1;

	/// <summary>Seconds left in the current sweep window.</summary>
	public double SweepSecondsRemaining { get; set; }

	/// <summary>Seconds the current ingress-attraction window remains open; 0 = closed.</summary>
	public double IngressAttractRemaining { get; set; }

	/// <summary>Seconds an ingress point remains banned after use, by ingress id (sorted).</summary>
	public SortedDictionary<string, double> IngressBanRemaining { get; set; } = new SortedDictionary<string, double>( System.StringComparer.Ordinal );

	/// <summary>Recent reason codes for diagnostics (bounded, oldest dropped first).</summary>
	public List<string> RecentReasons { get; set; } = new List<string>();

	/// <summary>Maximum length of <see cref="RecentReasons"/>.</summary>
	public const int MaxRecentReasons = 16;
}
