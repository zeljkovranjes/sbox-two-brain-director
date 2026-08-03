using System.Collections.Generic;

namespace TwoBrains.Core.Contract;

/// <summary>
/// An offstage (non-visible) area the monster can occupy for staging/sweeps, with the
/// nodes and ingress points the host approves for it. The core treats membership as the
/// only spatial truth about offstage space.
/// </summary>
public sealed class OffstageRegion
{
	public string RegionId { get; set; } = "";

	/// <summary>Node ids (see <see cref="NavCandidate.NodeId"/>) inside this region.</summary>
	public List<string> NodeIds { get; set; } = new List<string>();

	/// <summary>Ingress ids (see <see cref="IngressPoint.IngressId"/>) leading into this region.</summary>
	public List<string> IngressIds { get; set; } = new List<string>();

	/// <summary>Frontstage region ids this offstage region is adjacent to (host topology).</summary>
	public List<string> AdjacentRegionIds { get; set; } = new List<string>();
}
