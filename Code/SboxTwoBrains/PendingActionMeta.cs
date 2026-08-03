using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Serializable bookkeeping for one outstanding <see cref="ActionRequest"/>: its kind, the
/// original timeout interval in ticks (used to extend a Deferred action exactly once) and the
/// key identifying parameters (used for ingress bans, duplicate detection and lapse handling).
/// </summary>
public sealed class PendingActionMeta
{
	public ActionKind Kind { get; set; }

	/// <summary>Original expiry interval in ticks (ExpiryTick minus issuing tick).</summary>
	public long IntervalTicks { get; set; }

	public string TargetId { get; set; } = "";
	public string NodeId { get; set; } = "";
	public string RegionId { get; set; } = "";
	public string IngressId { get; set; } = "";
	public string StimulusId { get; set; } = "";
	public string Param { get; set; } = "";
	public Vec3? Destination { get; set; }
}
