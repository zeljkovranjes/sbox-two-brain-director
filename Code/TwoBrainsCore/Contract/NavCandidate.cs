namespace TwoBrains.Core.Contract;

/// <summary>What kind of navigation element a candidate represents.</summary>
public enum NavCandidateKind
{
	FrontstageNode = 0,
	OffstageNode = 1,
	/// <summary>A traversal point between stages; see <see cref="IngressPoint"/>.</summary>
	Ingress = 2,
}

/// <summary>
/// One host-supplied navigation option. The core never queries a navmesh; all reachability,
/// distance and visibility facts come from the host in this record.
/// </summary>
public sealed class NavCandidate
{
	/// <summary>Stable node id.</summary>
	public string NodeId { get; set; } = "";

	public NavCandidateKind Kind { get; set; } = NavCandidateKind.FrontstageNode;
	public Vec3 Position { get; set; }
	public string RegionId { get; set; } = "";

	/// <summary>Host routing fact: a path from the monster to this node exists now.</summary>
	public bool Reachable { get; set; } = true;

	/// <summary>Host-computed route distance in metres (not straight-line). &lt; 0 = unknown.</summary>
	public double RouteDistance { get; set; } = -1.0;

	/// <summary>Host LOS fact from the monster to this node.</summary>
	public bool HasLineOfSight { get; set; }

	/// <summary>For Kind == Ingress: id of the matching <see cref="IngressPoint"/>.</summary>
	public string IngressId { get; set; }

	/// <summary>Optional host cost bias; higher = less attractive. &gt;= 0.</summary>
	public double ExtraCost { get; set; }
}
