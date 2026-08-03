using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;
using Host = SboxTwoBrains.Tests.FakeHost.FakeHost;

namespace SboxTwoBrains.Tests.Integration;

/// <summary>
/// Replay and save/restore contract at system level: one scripted 300-tick scenario
/// (mixed attributed/unattributed stimuli, directives at fixed ticks, a Deferred-then-success
/// ack policy on MoveTo, damage, a forced withdrawal, an offstage sweep, a threat-gated
/// attack) must produce byte-identical canonical JSON and identical state hashes on re-run,
/// and a mid-run CaptureState → canonical-JSON round trip → RestoreState must continue with
/// output identical to the uninterrupted run.
///
/// Scenario shape (all real core behaviour): a forced opportunity at tick 0 with no memory
/// yet routes the sweeper role through the vent (Ambush needs memory evidence and a
/// frontstage presence, so it stays ineligible); the sweep is interrupted by an investigate
/// and a threat-gated chase because those modules outrank Offstage; the ambusher role never
/// engages because the monster is offstage for the rest of the run.
/// </summary>
public sealed class ReplayTests
{
	private const int Ticks = 300;
	private const long RestoreAt = 150;

	private static readonly Vec3 T1Pos = new Vec3( 15.0, 0.0, 0.0 );
	private static readonly Vec3 T2Pos = new Vec3( -2.0, 0.0, 12.0 );
	private static readonly Vec3 NoisePos = new Vec3( 4.0, 0.0, 0.0 );

	/// <summary>Builds the replay world: two targets, atrium search nodes, one vent into OFF1.</summary>
	private static Host NewReplayHost()
	{
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", T1Pos, "hall", threat: 0.1 ); // pressure candidate, never sensed
		var t2 = host.AddTarget( "t2", T2Pos, "atrium", threat: 0.8 );
		t2.IsArmed = true;

		IntegrationSupport.AddPlanarNode( host, "a1", 2.0, 2.0, "atrium" );
		IntegrationSupport.AddPlanarNode( host, "a2", -3.0, 1.0, "atrium" );
		IntegrationSupport.AddPlanarNode( host, "on1", 0.0, 30.0, "OFF1", kind: NavCandidateKind.OffstageNode );
		IntegrationSupport.AddPlanarNode( host, "on2", 5.0, 30.0, "OFF1", kind: NavCandidateKind.OffstageNode );
		host.AddIngress( "vent1", 3.0, 0.0, 0.0, "hall", "on1" );
		host.OffstageRegions.Add( new OffstageRegion
		{
			RegionId = "OFF1",
			NodeIds = { "on1", "on2" },
			IngressIds = { "vent1" },
			AdjacentRegionIds = { "hall" },
		} );
		// No frontstage nodes in "hall": frontstage stalk has nowhere to stand, so the
		// sweeper role must enter through the vent.

		// Ack policy under test: every MoveTo is Deferred once, then walked to success.
		var deferredOnce = new HashSet<string>( System.StringComparer.Ordinal );
		host.Policies[ActionKind.MoveTo] = ( request, h ) =>
			deferredOnce.Add( request.ActionId ) ? ActionStatus.Deferred : ActionStatus.Succeeded;
		return host;
	}

	/// <summary>The scripted input schedule, keyed purely on the tick index.</summary>
	private static void ApplySchedule( Host host )
	{
		long t = host.TickIndex;
		if ( t == 0 )
			IntegrationSupport.Direct( host, IntegrationSupport.ForceOpportunity( "hall" ) );
		if ( t == 60 || t == 61 )
			host.EmitNoise( "noise-60", NoisePos, "atrium", 0.8 );
		if ( t == 100 )
			IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.5, ResetGauge = true } );
		if ( t == 130 )
			host.DamageMonster( 0.2 );
		if ( t == 160 )
			IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.ForceWithdrawal } );
		if ( t >= 215 && t <= 280 )
			host.EmitVisual( "vis-t2", "t2", T2Pos, "atrium", 0.9 );
		if ( t == 250 )
			IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Normal } );
	}

	/// <summary>Runs the scenario; <paramref name="midRunHook"/> fires once when the given tick is reached.</summary>
	private static void RunScenario( List<string> json, List<ulong> hashes, long hookAtTick = -1, System.Action<Host> midRunHook = null )
	{
		var host = NewReplayHost();
		for ( int i = 0; i < Ticks; i++ )
		{
			if ( host.TickIndex == hookAtTick )
				midRunHook?.Invoke( host );
			ApplySchedule( host );
			var batch = host.Step();
			json.Add( CanonicalJson.ToJson( batch ) );
			hashes.Add( batch.StateHash );
		}
	}

	private static void AssertEventful( List<string> json )
	{
		// The run must actually exercise the systems, or the equality proves nothing.
		Assert.Contains( json, j => j.Contains( "\"Kind\":\"UseIngress\"" ) );
		Assert.Contains( json, j => j.Contains( "sweep_move" ) );
		Assert.Contains( json, j => j.Contains( "sweep_dwell" ) );
		Assert.Contains( json, j => j.Contains( "investigate_react" ) );
		Assert.Contains( json, j => j.Contains( "\"Kind\":\"Chase\"" ) );
		Assert.Contains( json, j => j.Contains( "attack_commit" ) );
		Assert.Contains( json, j => j.Contains( "retreat_start" ) );
		Assert.Contains( json, j => j.Contains( "script_forced_opportunity" ) );
		Assert.Contains( json, j => j.Contains( "opportunity_offered" ) );
		Assert.Contains( json, j => j.Contains( "script_withdrawal" ) );
	}

	[Fact]
	public void SameInputs_ProduceByteIdenticalBatchesAndHashes()
	{
		var jsonA = new List<string>();
		var hashA = new List<ulong>();
		var jsonB = new List<string>();
		var hashB = new List<ulong>();

		RunScenario( jsonA, hashA );
		RunScenario( jsonB, hashB );

		AssertEventful( jsonA );
		Assert.Equal( jsonA, jsonB );
		Assert.Equal( hashA, hashB );
	}

	[Fact]
	public void SaveRestoreContinuation_MatchesUninterruptedRun()
	{
		var expectedJson = new List<string>();
		var expectedHash = new List<ulong>();
		RunScenario( expectedJson, expectedHash );
		AssertEventful( expectedJson );

		var actualJson = new List<string>();
		var actualHash = new List<ulong>();
		string capturedJson = null;
		RunScenario( actualJson, actualHash, RestoreAt, host =>
		{
			// Capture, serialize, re-parse, restore: continuation must go through the JSON path.
			var envelope = host.System.CaptureState();
			capturedJson = CanonicalJson.ToJson( envelope );
			var parsed = CanonicalJson.FromJson<SavedStateEnvelope>( capturedJson );
			Assert.Equal( capturedJson, CanonicalJson.ToJson( parsed ) ); // canonical round trip is byte-stable
			Assert.Equal( RestoreAt - 1, parsed.TickIndex );
			host.System.RestoreState( parsed );
			// Re-capturing the restored state must reproduce the identical envelope bytes.
			Assert.Equal( capturedJson, CanonicalJson.ToJson( host.System.CaptureState() ) );
		} );

		Assert.NotNull( capturedJson );
		Assert.Equal( expectedJson, actualJson );
		Assert.Equal( expectedHash, actualHash );
	}
}
