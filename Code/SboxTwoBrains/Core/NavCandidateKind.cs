namespace SboxTwoBrains.Core;

/// <summary>What kind of navigation element a candidate represents.</summary>
public enum NavCandidateKind
{
	FrontstageNode = 0,
	OffstageNode = 1,
	/// <summary>A traversal point between stages; see <see cref="IngressPoint"/>.</summary>
	Ingress = 2,
}
