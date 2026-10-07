namespace SboxTwoBrains.Core;

/// <summary>Chase/attack execution guards.</summary>
public sealed class CombatSection
{
	/// <summary>Route distance within which an attack may commit (metres). Range: [0.1, 100].</summary>
	public double? AttackRange { get; set; }
	/// <summary>Abandon chase beyond this route distance (metres). Range: [1, 1000].</summary>
	public double? ChaseGiveUpDistance { get; set; }
	/// <summary>Abandon chase after losing the target this long (seconds). Range: [0.1, 600].</summary>
	public double? ChaseGiveUpSeconds { get; set; }
	/// <summary>Minimum time between attack commits. Range: [0, 600].</summary>
	public double? AttackCooldownSeconds { get; set; }
	/// <summary>Time budget for an ingress-flank manoeuvre. Range: [0, 600].</summary>
	public double? FlankIngressSeconds { get; set; }
	/// <summary>After a failed/rejected attack, wait this long before retrying. Range: [0, 600].</summary>
	public double? AttackBanSeconds { get; set; }
}
