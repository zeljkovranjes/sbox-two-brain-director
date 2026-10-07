namespace SboxTwoBrains.Core;

/// <summary>Offstage staging, sweep and ingress policy for the micro layer.</summary>
public sealed class OffstageSection
{
	/// <summary>After using an ingress, ignore it for this long (seconds). Range: [0, 600].</summary>
	public double? IngressBanSeconds { get; set; }
	/// <summary>Dwell at an offstage node, lower bound (seconds). Range: [0, 600].</summary>
	public double? NodeDwellMinSeconds { get; set; }
	/// <summary>Dwell at an offstage node, upper bound (seconds). Range: [0, 600].</summary>
	public double? NodeDwellMaxSeconds { get; set; }
	/// <summary>Prefer ingress points nearer the pressured region over nearer the monster.</summary>
	public bool? PreferIngressNearPressure { get; set; }
	/// <summary>Allow killtrap-style staged waiting at egress points.</summary>
	public bool? KilltrapEnabled { get; set; }
	/// <summary>Seconds before an unanswered ingress request counts as failed. Range: [1, 120].</summary>
	public double? IngressTimeoutSeconds { get; set; }
}
