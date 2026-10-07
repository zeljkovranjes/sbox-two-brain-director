namespace SboxTwoBrains.Core;

/// <summary>Where the monster currently is, in staging terms.</summary>
public enum StagePresence
{
	Frontstage = 0,
	Offstage = 1,
	/// <summary>Currently traversing a host-approved ingress (e.g. vent) between stages.</summary>
	InIngress = 2,
}
