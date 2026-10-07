using System.Collections.Generic;
using System.Globalization;

namespace SboxTwoBrains.Core;

/// <summary>Where a piece of target evidence came from (current senses, memory, omniscience).</summary>
internal enum EvidenceSource
{
	None = 0,
	CurrentVisual = 1,
	CurrentOther = 2,
	Memory = 3,
	Omniscient = 4,
}
