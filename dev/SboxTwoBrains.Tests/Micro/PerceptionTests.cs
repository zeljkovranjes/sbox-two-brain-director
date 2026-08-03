using System.Linq;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Perception memory: merge/refresh, linear decay, eviction, max age, sense gates.</summary>
public sealed class PerceptionTests
{
	[Fact]
	public void MergeCreatesMemoryFromStimulus()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 5.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.9, targetId: "t1", x: 5.0 ) );
		d.StepOnce();

		Assert.Single( d.State.Memories );
		var mem = d.State.Memories[0];
		Assert.Equal( "s1", mem.StimulusId );
		Assert.Equal( 0.9, mem.BaseConfidence );
		Assert.Equal( 0.9, mem.DecayedConfidence );
		Assert.True( mem.ConfirmedThisTick );
		Assert.Equal( 0, mem.LastConfirmedTick );
		Assert.Equal( "t1", d.State.CurrentTargetId );
		Assert.Equal( 0, d.State.LastSensedTargetTick );
		Assert.Equal( new Vec3( 5.0, 0.0, 0.0 ), d.State.LastSensedTargetPosition );
	}

	[Fact]
	public void MergeRefreshesExistingMemory()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 5.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.9, targetId: "t1", x: 5.0 ) );
		d.StepOnce(); // tick 0: created
		d.World.CurrentStimuli.Clear();
		d.StepOnce(); // tick 1: decays
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.5, targetId: "t1", x: 7.0 ) );
		d.StepOnce(); // tick 2: refreshed

		var mem = Assert.Single( d.State.Memories );
		Assert.Equal( 0.5, mem.BaseConfidence );
		Assert.Equal( 0.5, mem.DecayedConfidence );
		Assert.Equal( new Vec3( 7.0, 0.0, 0.0 ), mem.Position );
		Assert.Equal( 2, mem.LastConfirmedTick );
		Assert.True( mem.ConfirmedThisTick );
	}

	[Fact]
	public void DecayIsLinearAndExact()
	{
		var d = new AgentDriver();
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 5.0 ) );
		d.StepOnce(); // tick 0: confirmed, no decay yet
		d.World.CurrentStimuli.Clear();
		d.Step( 10 ); // ticks 1..10: ten unconfirmed decays

		// rate = dt * (base / (2 * half-life)) = 0.1 * (1.0 / 60.0) per tick, applied iteratively
		double expected = 1.0;
		for ( int i = 0; i < 10; i++ ) expected -= 0.1 * ( 1.0 / ( 2.0 * 30.0 ) );
		var mem = Assert.Single( d.State.Memories );
		Assert.Equal( expected, mem.DecayedConfidence, 12 );
		Assert.True( mem.DecayedConfidence > 0.98 );
	}

	[Fact]
	public void CapacityEvictionUsesConfidenceThenAgeThenOrdinal()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Perception.MemoryCapacity = 2;
		var d = new AgentDriver( 1, cfg );

		// exact tie on confidence -> oldest confirmation evicted
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Auditory, 0.9 ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s3", SenseChannel.Auditory, 0.85 ) );
		d.StepOnce(); // tick 0: {s1, s3}
		d.World.CurrentStimuli.Clear();
		d.World.CurrentStimuli.Add( Snap.Stim( "s4", SenseChannel.Auditory, 0.9 ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s5", SenseChannel.Auditory, 0.9 ) );
		d.StepOnce(); // tick 1: 4 > 2 -> evict s3 (lowest), then s1 (tie 0.9, oldest tick)
		Assert.Equal( new[] { "s4", "s5" }, d.State.Memories.Select( m => m.StimulusId ).OrderBy( x => x, System.StringComparer.Ordinal ).ToArray() );

		// exact tie on confidence AND age -> ordinal id evicted
		var cfg2 = new SboxTwoBrains.EffectiveConfig();
		cfg2.Perception.MemoryCapacity = 2;
		var d2 = new AgentDriver( 1, cfg2 );
		d2.World.CurrentStimuli.Add( Snap.Stim( "a1", SenseChannel.Auditory, 0.9 ) );
		d2.World.CurrentStimuli.Add( Snap.Stim( "a2", SenseChannel.Auditory, 0.9 ) );
		d2.StepOnce(); // tick 0: {a1, a2} both 0.9 @ t0
		d2.World.CurrentStimuli.Clear();
		d2.World.CurrentStimuli.Add( Snap.Stim( "a3", SenseChannel.Auditory, 0.9 ) );
		d2.StepOnce(); // evict a1 (tie conf, tie age handled by oldest, then ordinal)
		Assert.Equal( new[] { "a2", "a3" }, d2.State.Memories.Select( m => m.StimulusId ).OrderBy( x => x, System.StringComparer.Ordinal ).ToArray() );
	}

	[Fact]
	public void MaxAgeDropsMemory()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Perception.Visual.MaxAgeSeconds = 1.0;
		var d = new AgentDriver( 1, cfg );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.9, targetId: "t1", x: 5.0 ) );
		d.StepOnce(); // tick 0
		d.World.CurrentStimuli.Clear();
		d.Step( 10 ); // ticks 1..10 -> age exactly 1.0 s: not yet beyond MaxAge
		Assert.Single( d.State.Memories );
		d.StepOnce(); // tick 11 -> age 1.1 s > MaxAge
		Assert.Empty( d.State.Memories );
	}

	[Fact]
	public void SenseActivationHonoursThresholdAndLatch()
	{
		var d = new AgentDriver();
		// below threshold: inactive
		d.World.CurrentStimuli.Add( Snap.Stim( "s0", SenseChannel.Visual, 0.2 ) );
		Assert.False( d.Probe().SenseActive( SenseChannel.Visual ) );
		d.World.CurrentStimuli.Clear();
		// at/above threshold: active
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.5 ) );
		Assert.True( d.Probe().SenseActive( SenseChannel.Visual ) );
		d.StepOnce(); // tick 0: memory confirmed
		d.World.CurrentStimuli.Clear();
		d.Step( 3 ); // ticks 1..3 -> 0.3 s since confirmation: latch holds (<= 0.5 s)
		Assert.True( d.Probe().SenseActive( SenseChannel.Visual ) );
		d.Step( 3 ); // ticks 4..6 -> 0.6 s: latch released (memory still above threshold)
		Assert.False( d.Probe().SenseActive( SenseChannel.Visual ) );
	}

	[Fact]
	public void CurrentAndRememberedEvidenceStayDistinct()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 5.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 5.0 ) );
		d.StepOnce(); // tick 0: current evidence -> attack motivation
		Assert.Contains( "attack", d.State.Motivations );
		Assert.DoesNotContain( "search", d.State.Motivations );

		d.World.CurrentStimuli.Clear();
		d.StepOnce(); // tick 1: remembered only -> search motivation, target retained
		Assert.Contains( "search", d.State.Motivations );
		Assert.DoesNotContain( "attack", d.State.Motivations );
		Assert.Equal( "t1", d.State.CurrentTargetId );
		Assert.False( Assert.Single( d.State.Memories ).ConfirmedThisTick );
	}

	[Fact]
	public void OmniscienceEmitsOncePerActivationEdge()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0 ) );
		d.World.OmniscientTargets = true;
		d.StepOnce(); // tick 0: edge on
		Assert.Equal( 1, d.HistCount( "omniscience_active" ) );
		Assert.Equal( "t1", d.State.CurrentTargetId );
		Assert.Equal( ActionKind.Attack, d.LastActions[0].Kind ); // omniscient evidence counts as current
		d.StepOnce(); // tick 1: still on, no repeat
		Assert.Equal( 1, d.HistCount( "omniscience_active" ) );

		d.World.OmniscientTargets = false;
		d.StepOnce(); // tick 2: edge off (silent)
		d.World.OmniscientTargets = true;
		d.StepOnce(); // tick 3: second activation
		Assert.Equal( 2, d.HistCount( "omniscience_active" ) );
	}
}
