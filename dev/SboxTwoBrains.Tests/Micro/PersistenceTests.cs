using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Save/restore continuation and double-run replay determinism.</summary>
public sealed class PersistenceTests
{
	/// <summary>
	/// Deterministic per-tick input script (world mutations, fixed-id acks, one macro bias,
	/// one directive). Acks reference ids by the "a{tick}-{n}" convention so the identical
	/// input stream can be replayed after a restore.
	/// </summary>
	private static void Script( AgentDriver d )
	{
		var w = d.World;
		long t = d.Tick;
		if ( t == 0 )
		{
			w.Targets.Add( Snap.Target( "t1", 6.0, 0.0, threat: 0.8, visible: true ) );
			w.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 6.0 ) );
			w.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0, route: 5.0 ) );
			w.IngressPoints.Add( Snap.Ingress( "ing1", 5.0, 0.0, region: "R1" ) );
		}
		if ( t == 8 ) w.CurrentStimuli.Clear();
		if ( t == 12 ) w.CurrentStimuli.Add( Snap.Stim( "st2", SenseChannel.Auditory, 0.9, x: 20.0, region: "R2" ) );
		if ( t == 20 ) w.CurrentStimuli.Clear();
		if ( t == 25 ) d.Macro = Snap.Macro( "R1", roles: new[] { "stalker" } );
		if ( t == 40 ) w.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 6.0 ) );
		if ( t == 50 ) d.Direct( new ScriptDirective { DirectiveId = "d1", Kind = ScriptDirectiveKind.ForceWithdrawal } );
		// deterministic ack policy: try to complete the action issued two ticks ago
		if ( t >= 2 ) d.Ack( "a" + ( t - 2 ) + "-0", ActionStatus.Succeeded );
	}

	private static List<string> Run( AgentDriver d, int from, int to )
	{
		var outputs = new List<string>();
		for ( int t = from; t < to; t++ )
		{
			Script( d );
			d.StepOnce();
			outputs.Add( d.OutputJson() );
		}
		return outputs;
	}

	[Fact]
	public void CaptureRestoreRoundTripsCanonically()
	{
		var a = new AgentDriver( 42 );
		Run( a, 0, 20 );
		string json = a.Agent.CaptureState();
		var ( s0, s1 ) = a.Rng.GetState();

		var b = new AgentDriver( 42 );
		b.Rng.SetState( s0, s1 );
		b.Agent.RestoreState( json );
		Assert.Equal( json, b.Agent.CaptureState() );
	}

	[Fact]
	public void RestoreAtTick30ContinuesIdentically()
	{
		var a = new AgentDriver( 42 );
		var outputsA = Run( a, 0, 60 );

		var b = new AgentDriver( 42 );
		var firstHalfB = Run( b, 0, 30 );
		Assert.Equal( outputsA.GetRange( 0, 30 ), firstHalfB ); // same seed + inputs -> same output

		string saved = b.Agent.CaptureState();
		var ( s0, s1 ) = b.Rng.GetState();

		var restored = new AgentDriver( 42 );
		restored.Rng.SetState( s0, s1 );
		restored.Agent.RestoreState( saved );
		restored.Tick = 30;
		restored.World = b.World; // host-owned world continues; identical input stream
		restored.Macro = b.Macro;
		var secondHalf = Run( restored, 30, 60 );

		Assert.Equal( outputsA.GetRange( 30, 30 ), secondHalf );
	}

	[Fact]
	public void IdenticalRunsProduceIdenticalCanonicalOutput()
	{
		var a = new AgentDriver( 7 );
		var b = new AgentDriver( 7 );
		var outA = Run( a, 0, 100 );
		var outB = Run( b, 0, 100 );
		Assert.Equal( outA, outB );
		Assert.Equal( a.Agent.CaptureState(), b.Agent.CaptureState() );
	}
}
