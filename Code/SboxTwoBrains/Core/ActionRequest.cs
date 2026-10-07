namespace SboxTwoBrains.Core;

/// <summary>
/// One declarative action request. Ids are deterministic ("a{tick}-{ordinal}").
/// The host executes it and acknowledges with an <see cref="ActionResult"/> on a later tick.
/// </summary>
public sealed class ActionRequest
{
	public string ActionId { get; set; } = "";
	public ActionKind Kind { get; set; }

	/// <summary>MoveTo/Chase/Retreat destination (metres), when relevant.</summary>
	public Vec3? Destination { get; set; }

	/// <summary>Region scope for Search/Stalk/Sweep-style actions.</summary>
	public string RegionId { get; set; }

	/// <summary>Specific nav node to use, when the module selected one.</summary>
	public string NodeId { get; set; }

	/// <summary>Ingress id for UseIngress.</summary>
	public string IngressId { get; set; }

	/// <summary>Participant id for Chase/Attack/Threat/Stalk.</summary>
	public string TargetId { get; set; }

	/// <summary>Stimulus/memory id for Investigate.</summary>
	public string StimulusId { get; set; }

	/// <summary>Desired movement speed scale in [0,1] (host maps to locomotion).</summary>
	public double SpeedScale { get; set; } = 1.0;

	/// <summary>Custom payload (sequence name, game-defined action data).</summary>
	public string Param { get; set; }

	/// <summary>Tick after which the host should treat the request as lapsed.</summary>
	public long ExpiryTick { get; set; }

	/// <summary>Machine-readable reason (module + gate) for diagnostics.</summary>
	public string ReasonCode { get; set; } = "";
}
