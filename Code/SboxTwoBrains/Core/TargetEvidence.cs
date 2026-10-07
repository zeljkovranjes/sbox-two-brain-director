using System.Collections.Generic;
using System.Globalization;

namespace SboxTwoBrains.Core;

/// <summary>One resolved piece of evidence (target-attributed or unattributed).</summary>
internal sealed class TargetEvidence
{
	public EvidenceSource Source = EvidenceSource.None;
	public string TargetId = "";
	public string StimulusId = "";
	public Vec3 Position;
	public string RegionId = "";
	public double Confidence;
	public SenseChannel Channel;

	/// <summary>True when this evidence comes from the current tick (not memory).</summary>
	public bool IsCurrent => Source == EvidenceSource.CurrentVisual || Source == EvidenceSource.CurrentOther || Source == EvidenceSource.Omniscient;
}
