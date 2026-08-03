using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Perception memory maintenance. Current evidence (this tick's stimuli) and remembered
/// evidence (decaying <see cref="MemoryRecord"/>s) stay distinct: merging refreshes or
/// creates records, decay lowers confidence linearly per channel half-life, and records are
/// dropped at zero confidence or beyond their channel max age. All rules are deterministic;
/// capacity eviction uses (confidence, age, ordinal id) ordering.
/// </summary>
internal static class Perception
{
	/// <summary>Clears per-tick confirmation flags before the merge pass re-marks them.</summary>
	public static void ClearConfirmations( MicroState state )
	{
		foreach ( var m in state.Memories )
			if ( m != null ) m.ConfirmedThisTick = false;
	}

	public static MemoryRecord FindMemory( MicroState state, string stimulusId )
	{
		foreach ( var m in state.Memories )
			if ( m != null && m.StimulusId == stimulusId ) return m;
		return null;
	}

	/// <summary>Merges current stimuli into memory (refresh or insert) and enforces capacity.</summary>
	public static void Merge( AgentContext ac )
	{
		var state = ac.State;
		foreach ( var s in ac.World.CurrentStimuli )
		{
			if ( s == null || string.IsNullOrEmpty( s.StimulusId ) ) continue;
			var mem = FindMemory( state, s.StimulusId );
			if ( mem != null )
			{
				mem.Channel = s.Channel;
				mem.Subtype = s.Subtype;
				mem.Position = s.Position;
				mem.RegionId = s.RegionId ?? "";
				mem.BaseConfidence = s.Confidence;
				mem.DecayedConfidence = s.Confidence;
				mem.TargetId = s.TargetId;
				mem.LastConfirmedTick = ac.TickIndex;
				mem.ConfirmedThisTick = true;
			}
			else
			{
				state.Memories.Add( new MemoryRecord
				{
					StimulusId = s.StimulusId,
					Channel = s.Channel,
					Subtype = s.Subtype,
					Position = s.Position,
					RegionId = s.RegionId ?? "",
					BaseConfidence = s.Confidence,
					DecayedConfidence = s.Confidence,
					TargetId = s.TargetId,
					CreatedTick = s.CreatedTick,
					LastConfirmedTick = ac.TickIndex,
					ConfirmedThisTick = true,
				} );
			}
		}

		int capacity = ac.Cfg.Perception.MemoryCapacity;
		while ( state.Memories.Count > capacity )
		{
			// evict lowest decayed confidence; ties: oldest confirmation, then ordinal id
			int evict = 0;
			for ( int i = 1; i < state.Memories.Count; i++ )
			{
				var a = state.Memories[i];
				var b = state.Memories[evict];
				if ( a.DecayedConfidence < b.DecayedConfidence
					|| ( a.DecayedConfidence == b.DecayedConfidence && a.LastConfirmedTick < b.LastConfirmedTick )
					|| ( a.DecayedConfidence == b.DecayedConfidence && a.LastConfirmedTick == b.LastConfirmedTick
						&& string.CompareOrdinal( a.StimulusId, b.StimulusId ) < 0 ) )
					evict = i;
			}
			state.Memories.RemoveAt( evict );
		}
	}

	/// <summary>
	/// Linear decay for unconfirmed memories: loses BaseConfidence / (2 × half-life) per
	/// second (no transcendental math), then drops records at zero or past MaxAgeSeconds.
	/// </summary>
	public static void DecayAndPrune( AgentContext ac )
	{
		var state = ac.State;
		double dt = ac.Dt;
		for ( int i = state.Memories.Count - 1; i >= 0; i-- )
		{
			var mem = state.Memories[i];
			if ( mem == null )
			{
				state.Memories.RemoveAt( i );
				continue;
			}
			var ch = ac.ChannelCfg( mem.Channel );
			if ( !mem.ConfirmedThisTick )
			{
				mem.DecayedConfidence -= dt * ( mem.BaseConfidence / ( 2.0 * ch.DecayHalfLifeSeconds ) );
				if ( mem.DecayedConfidence < 0.0 ) mem.DecayedConfidence = 0.0;
			}
			double ageSeconds = ( ac.TickIndex - mem.LastConfirmedTick ) * dt;
			if ( mem.DecayedConfidence <= 0.0 || ageSeconds > ch.MaxAgeSeconds )
				state.Memories.RemoveAt( i );
		}
	}
}
