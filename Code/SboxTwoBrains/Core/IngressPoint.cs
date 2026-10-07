namespace SboxTwoBrains.Core;

/// <summary>
/// A host-approved transition point between frontstage and offstage areas.
/// Offstage repositioning may only happen through these; every use is logged with
/// source, destination, kind, reason, and the host's acknowledgement.
/// </summary>
public sealed class IngressPoint
{
	public string IngressId { get; set; } = "";
	public IngressKind Kind { get; set; } = IngressKind.Vent;
	public Vec3 Position { get; set; }
	public string RegionId { get; set; } = "";

	/// <summary>Offstage node (a <see cref="NavCandidate.NodeId"/>) this ingress connects to.</summary>
	public string OffstageNodeId { get; set; } = "";

	/// <summary>Host feasibility fact: traversal is possible right now.</summary>
	public bool Usable { get; set; } = true;

	/// <summary>Host-side cooldown: not usable again until this tick. -1 = no cooldown.</summary>
	public long CooldownUntilTick { get; set; } = -1;
}
