using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Integration;

/// <summary>
/// End-to-end scenarios: a TwoBrainsSystem on the preset catalogue's DEFAULT profile
/// (seed 42, dt 1/20 unless noted) driven through the fake host. Assertions read
/// observable output only — actions, telemetry codes and macro decisions in the batches —
/// plus public system state where the contract exposes it (progression, counts, mode).
///
/// Two DEFAULT-profile facts shape several scenarios (trusting the core over intuition):
///  * AggressiveThresholdProgression is 1.0 and the fill is asymptotic, so a purely natural
///    fill at dt 1/20 never reaches the threshold in floating point; transitions are driven
///    by coarse dt (clamp), seeded progression, or script directives.
///  * DecreaseDelaySeconds is 0, so gauge decrease begins the same tick the latch is lost.
/// </summary>
public sealed class ScenarioTests
{
	private const double Dt = IntegrationSupport.Dt;

	// 1 — target present, no stimuli: gauge fills toward the threshold over ~FillSeconds.
	[Fact]
	public void CalmToPressureProgression()
	{
		// Part A — fill curve at dt 1/20: asymptotic, matches the recurrence exactly.
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		double fill = host.System.Config.Pressure.FillSeconds; // 3s in DEFAULT

		var batches = new List<DecisionBatch>();
		double expected = 0.0;
		for ( int i = 0; i < 60; i++ ) // 60 ticks * 0.05s = 3s = FillSeconds
		{
			batches.Add( host.Step() );
			expected = expected + ( 1.0 - expected ) / fill * Dt;
			Assert.Equal( expected, host.System.MacroState.Progression );
		}
		foreach ( var b in batches )
			Assert.Null( b.Macro ); // latch/fill alone never emits a decision
		Assert.InRange( host.System.MacroState.Progression, 0.63, 0.64 ); // ~1 - 1/e after FillSeconds
		Assert.True( host.System.MacroState.CandidateLatched );
		Assert.Equal( "hall", host.System.MacroState.ActiveCandidateId );
		Assert.Equal( PressureMode.Normal, host.System.MacroState.Mode );
		Assert.Equal( 1, IntegrationSupport.CountCode( batches, "candidate_latched" ) );
		Assert.Equal( 0, IntegrationSupport.CountCode( batches, "opportunity_offered" ) );

		// Part B — transition with progression ~1: at a coarse dt the fill increment
		// overshoots and Clamp01 pins the gauge to exactly 1.0, crossing threshold 1.0.
		var coarse = IntegrationSupport.NewHost( dt: 4.0 );
		coarse.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		var first = coarse.Step();
		Assert.NotNull( first.Macro );
		Assert.Equal( "mode_aggressive_start", first.Macro.ReasonCode );
		Assert.Equal( PressureMode.Aggressive, first.Macro.Mode );
		Assert.Equal( 1.0, first.Macro.Progression );
		Assert.Equal( 1.0, first.Macro.Urgency );
		Assert.Equal( "hall", first.Macro.CandidateRegionId );
		Assert.Equal( "op0-0", first.Macro.OpportunityId );
		Assert.True( first.Macro.ExpiryTick > first.TickIndex );
		Assert.True( IntegrationSupport.HasCode( new[] { first }, "opportunity_offered" ) );
		Assert.True( IntegrationSupport.HasCode( new[] { first }, "candidate_latched" ) );
	}

	// 2 — SetPressureMode(aggressive) flips the mode with script telemetry + a decision.
	[Fact]
	public void ScriptedAggressiveMode()
	{
		// Without ResetGauge the scripted aggressive mode arms no sweep window, so the
		// core discharges it immediately: script_set_mode, then opportunity_completed.
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		host.Run( 5 );
		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.5 } );
		var batch = host.Step();

		Assert.True( IntegrationSupport.HasCode( new[] { batch }, "script_set_mode" ) );
		Assert.Contains( "mode=Aggressive", batch.Telemetry.Find( e => e.Code == "script_set_mode" ).Message );
		Assert.NotNull( batch.Macro ); // the immediate discharge is itself a decision
		Assert.Equal( "opportunity_completed", batch.Macro.ReasonCode );
		Assert.Equal( PressureMode.Normal, batch.Macro.Mode ); // flipped aggressive, completed, back to normal
		Assert.Equal( 1, host.System.MacroState.CompletedOpportunities );
		Assert.True( host.System.MacroState.CooldownRemaining > 0.0 );

		// With ResetGauge the reset arms the sweep: mode stays aggressive, offer is pending.
		var host2 = IntegrationSupport.NewHost();
		host2.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		host2.Run( 5 );
		IntegrationSupport.Direct( host2, new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.5, ResetGauge = true } );
		var resetBatch = host2.Step();

		Assert.True( IntegrationSupport.HasCode( new[] { resetBatch }, "script_set_mode" ) );
		Assert.True( IntegrationSupport.HasCode( new[] { resetBatch }, "reset" ) );
		Assert.True( IntegrationSupport.HasCode( new[] { resetBatch }, "opportunity_offered" ) );
		Assert.NotNull( resetBatch.Macro );
		Assert.Equal( "reset", resetBatch.Macro.ReasonCode );
		Assert.Equal( PressureMode.Aggressive, resetBatch.Macro.Mode );
		Assert.Equal( "hall", resetBatch.Macro.CandidateRegionId ); // re-latched after the reset cleared it

		var follow = host2.Run( 20 );
		foreach ( var b in follow )
			Assert.Null( b.Macro ); // long sweep: no completion, no expiry inside 20 ticks
		Assert.Equal( PressureMode.Aggressive, host2.System.MacroState.Mode );
		Assert.True( host2.System.MacroState.SweepSecondsRemaining > 0.0 );
		Assert.True( host2.System.MacroState.PendingOpportunityId.Length > 0 );
	}

	// 3 — MaxOpportunities completed: quota_blocked, no new aggressive until a reset directive.
	[Fact]
	public void QuotaExhaustion()
	{
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		host.OpportunityPolicy = ( opportunity, h, postponements ) => ActionStatus.Succeeded;
		int max = host.System.Config.Pressure.MaxOpportunities; // 4 in DEFAULT

		// Drive MaxOpportunities completions via forced opportunities with instant host acks.
		for ( int i = 0; i < max; i++ )
		{
			IntegrationSupport.Direct( host, IntegrationSupport.ForceOpportunity( "hall" ) );
			var offer = host.Step();
			Assert.True( IntegrationSupport.HasCode( new[] { offer }, "opportunity_offered" ) );
			var completion = host.Step();
			Assert.True( IntegrationSupport.HasCode( new[] { completion }, "opportunity_completed" ) );
		}
		Assert.Equal( max, host.System.MacroState.CompletedOpportunities );

		// Age the post-completion cooldown (25s = 500 ticks at dt 1/20).
		var quiet = host.Run( 520 );
		Assert.Equal( 0, IntegrationSupport.CountCode( quiet, "opportunity_offered" ) );
		Assert.Equal( 0, IntegrationSupport.CountCode( quiet, "quota_blocked" ) ); // gauge stays below threshold
		Assert.True( host.System.MacroState.CooldownRemaining <= 0.0 );

		// Gauge at threshold with the quota spent: blocked, no offer, no decision.
		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 1.0 } );
		var blocked = new List<DecisionBatch> { host.Step() };
		blocked.AddRange( host.Run( 20 ) );
		Assert.True( IntegrationSupport.CountCode( blocked, "quota_blocked" ) > 0 );
		Assert.Equal( 0, IntegrationSupport.CountCode( blocked, "opportunity_offered" ) );
		Assert.All( blocked, b => Assert.Null( b.Macro ) );
		Assert.Equal( PressureMode.Normal, host.System.MacroState.Mode );
		Assert.Equal( max, host.System.MacroState.CompletedOpportunities );

		// A reset directive re-opens pressure; the gauge at threshold fires immediately.
		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure } );
		var reset = host.Step();
		Assert.True( IntegrationSupport.HasCode( new[] { reset }, "reset" ) );
		Assert.Equal( 0, host.System.MacroState.CompletedOpportunities );

		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 1.0 } );
		var reopened = host.Step();
		Assert.True( IntegrationSupport.HasCode( new[] { reopened }, "opportunity_offered" ) );
		Assert.NotNull( reopened.Macro );
		Assert.Equal( "mode_aggressive_start", reopened.Macro.ReasonCode );
	}

	// 4 — post-completion cooldown blocks re-transition; losing the candidate drains the gauge.
	[Fact]
	public void CooldownAndDecrease()
	{
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		host.OpportunityPolicy = ( opportunity, h, postponements ) => ActionStatus.Succeeded;

		// One forced cycle completes at tick 1: cooldown 25s starts, gauge resets to 0.
		IntegrationSupport.Direct( host, IntegrationSupport.ForceOpportunity( "hall" ) );
		host.Step();
		host.Step();
		Assert.Equal( 1, host.System.MacroState.CompletedOpportunities );

		// Seed the gauge at threshold while cooling down: the transition must not fire.
		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 1.0 } );
		host.Step();
		Assert.Equal( 1.0, host.System.MacroState.Progression );

		var cooling = new List<DecisionBatch>();
		long offerTick = -1;
		for ( int i = 0; i < 520 && offerTick < 0; i++ )
		{
			var b = host.Step();
			cooling.Add( b );
			if ( b.Macro != null && b.Macro.ReasonCode == "mode_aggressive_start" )
				offerTick = b.TickIndex;
		}
		Assert.True( offerTick > 0, "expected a natural aggressive start once the cooldown aged out" );
		Assert.True( offerTick >= 500, "cooldown 25s must block re-transition for ~500 ticks; fired at " + offerTick );
		Assert.Equal( 1.0, host.System.MacroState.Progression );
		Assert.Equal( 0, IntegrationSupport.CountCode( cooling.GetRange( 0, cooling.Count - 1 ), "opportunity_offered" ) );

		// The blocked offer completes via the host policy; re-seed the gauge for the decrease phase.
		host.Step();
		IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 0.6 } );
		host.Step();
		Assert.Equal( 0.6, host.System.MacroState.Progression );

		// Losing the only viable target clears the latch; DEFAULT's decrease delay is 0,
		// so the gauge drains immediately and floors at exactly 0.
		Assert.Equal( 0.0, host.System.Config.Pressure.DecreaseDelaySeconds );
		host.Targets.Clear();
		var draining = new List<DecisionBatch>();
		double previous = 0.6;
		long zeroTick = -1;
		for ( int i = 0; i < 260; i++ )
		{
			var b = host.Step();
			draining.Add( b );
			double p = host.System.MacroState.Progression;
			Assert.True( p <= previous, "gauge must decrease monotonically" );
			previous = p;
			if ( p == 0.0 && zeroTick < 0 )
				zeroTick = b.TickIndex;
		}
		Assert.True( IntegrationSupport.HasCode( draining, "candidate_cleared" ) );
		Assert.False( host.System.MacroState.CandidateLatched );
		Assert.True( zeroTick > 0, "gauge must reach exactly 0" );
		Assert.InRange( zeroTick - draining[0].TickIndex, 230, 245 ); // 0.6 at 0.05/20s per tick ≈ 240 ticks
		Assert.Equal( 0.0, host.System.MacroState.Progression );
		Assert.Equal( 0, IntegrationSupport.CountCode( draining, "opportunity_offered" ) );
	}

	// 5 — visual contact → chase/attack; losing sight → suspect response → systematic search.
	[Fact]
	public void LostSightIntoSearch()
	{
		var host = IntegrationSupport.NewHost();
		var target = host.AddTarget( "t1", new Vec3( 6.0, 0.0, 0.0 ), "hall" );
		IntegrationSupport.AddPlanarNode( host, "h1", 6.0, 2.0, "hall" );
		IntegrationSupport.AddPlanarNode( host, "h2", 12.0, 0.0, "hall" );
		IntegrationSupport.AddPlanarNode( host, "h3", 8.0, -4.0, "hall" );

		// Phase 1: keep the target visible — chase, then attack once inside attack range.
		var visible = new List<DecisionBatch>();
		long attackTick = -1;
		for ( int i = 0; i < 200 && attackTick < 0; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", target.Position, "hall" );
			var b = host.Step();
			visible.Add( b );
			if ( IntegrationSupport.HasCode( new[] { b }, "attack_commit" ) )
				attackTick = b.TickIndex;
		}
		Assert.True( attackTick > 0, "expected an attack_commit while the target was visible" );
		long chaseTick = IntegrationSupport.FirstTickOfCode( visible, "chase" );
		Assert.True( chaseTick >= 0 && chaseTick < attackTick, "chase must precede the attack" );
		Assert.Equal( 0, IntegrationSupport.CountActions( visible, ActionKind.Search ) );

		// Phase 2: no more stimuli — suspect response once, then systematic search of "hall".
		var blind = host.Run( 300 );
		long suspectTick = IntegrationSupport.FirstTickOfCode( blind, "suspect_response" );
		long searchStartTick = IntegrationSupport.FirstTickOfCode( blind, "search_start" );
		long searchEndTick = IntegrationSupport.FirstTickOfCode( blind, "search_end" );
		Assert.True( suspectTick >= 0, "expected a suspect response after losing sight" );
		Assert.True( searchStartTick > suspectTick, "search must follow the suspect response" );
		Assert.True( searchEndTick > searchStartTick, "the episode must end after exhausting nodes" );

		var searches = IntegrationSupport.AllActions( blind ).FindAll( a => a.Kind == ActionKind.Search );
		Assert.Equal( 3, searches.Count );
		Assert.Equal( "h1", searches[0].NodeId ); // nearest node to the monster first
		Assert.All( searches, a => Assert.Equal( "hall", a.RegionId ) );
		var nodes = new HashSet<string>();
		foreach ( var s in searches )
		{
			Assert.True( nodes.Add( s.NodeId ), "search must not revisit nodes inside one episode" );
			Assert.Contains( s.NodeId, new[] { "h1", "h2", "h3" } );
		}

		// No attack/chase after the stimuli stopped: only memory-driven behaviour remains.
		Assert.Equal( 0, IntegrationSupport.CountCode( blind, "attack_commit" ) );
		Assert.Equal( 0, IntegrationSupport.CountCode( blind, "chase" ) );
	}

	// 6 — unattributed noise → investigate stages react → approach → inspect → search.
	[Fact]
	public void StimulusInvestigation()
	{
		var host = IntegrationSupport.NewHost();
		IntegrationSupport.AddPlanarNode( host, "a1", 4.0, 2.0, "atrium" );
		IntegrationSupport.AddPlanarNode( host, "a2", 2.0, -2.0, "atrium" );
		host.EmitNoise( "noise-1", new Vec3( 4.0, 0.0, 0.0 ), "atrium", 0.8 );

		var batches = host.Run( 300 );
		var telemetry = IntegrationSupport.AllTelemetry( batches );
		var codes = new List<string>();
		foreach ( var e in telemetry )
			codes.Add( e.Code );

		// Stage order in the telemetry stream.
		int react = codes.IndexOf( "investigate_react" );
		int approach = codes.IndexOf( "investigate_approach" );
		int inspect = codes.IndexOf( "investigate_inspect" );
		int done = codes.IndexOf( "investigate_done" );
		int searchStart = codes.IndexOf( "search_start" );
		Assert.True( react >= 0 && approach > react && inspect > approach && done > inspect,
			"investigate stages must run react → approach → inspect → done" );
		Assert.True( searchStart > done, "systematic search starts after the investigate handoff" );

		// The staged actions themselves.
		var actions = IntegrationSupport.AllActions( batches );
		var reactWait = actions.Find( a => a.ReasonCode == "investigate_react" );
		var approachMove = actions.Find( a => a.ReasonCode == "investigate_approach" );
		var inspectWait = actions.Find( a => a.ReasonCode == "investigate_inspect" );
		Assert.NotNull( reactWait );
		Assert.Equal( ActionKind.Wait, reactWait.Kind );
		Assert.Equal( "0.5", reactWait.Param );
		Assert.NotNull( approachMove );
		Assert.Equal( ActionKind.MoveTo, approachMove.Kind );
		Assert.Equal( new Vec3( 4.0, 0.0, 0.0 ), approachMove.Destination.Value );
		Assert.NotNull( inspectWait );
		Assert.Equal( ActionKind.Wait, inspectWait.Kind );
		Assert.Equal( "2", inspectWait.Param ); // DEFAULT facing time is 2s

		// The search that follows targets the noise's region nodes.
		var searches = actions.FindAll( a => a.Kind == ActionKind.Search );
		Assert.True( searches.Count >= 1 );
		Assert.All( searches, a => Assert.Equal( "atrium", a.RegionId ) );
		Assert.Contains( searches, a => a.NodeId == "a1" );
		Assert.True( IntegrationSupport.HasCode( batches, "search_end" ) );

		// Nothing was ever attributed to a target: no chase, no attack.
		Assert.Equal( 0, IntegrationSupport.CountCode( batches, "attack_commit" ) );
		Assert.Equal( 0, IntegrationSupport.CountCode( batches, "chase" ) );
	}

	// 7 — aggressive macro + offstage adjacency: ingress entry, sweep dwells, exit.
	[Fact]
	public void VentIngressStalking()
	{
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 30.0, 0.0, 0.0 ), "hall" );
		host.AddIngress( "vent1", 2.0, 0.0, 0.0, "hall", "on1" );
		host.OffstageRegions.Add( new OffstageRegion
		{
			RegionId = "OFF1",
			NodeIds = { "on1", "on2" },
			IngressIds = { "vent1" },
			AdjacentRegionIds = { "hall" },
		} );
		IntegrationSupport.AddPlanarNode( host, "on1", 0.0, 3.0, "OFF1", kind: NavCandidateKind.OffstageNode );
		IntegrationSupport.AddPlanarNode( host, "on2", 5.0, 3.0, "OFF1", kind: NavCandidateKind.OffstageNode );
		// Deliberately no frontstage nodes in "hall": frontstage stalk has nowhere to stand,
		// so the sweeper role must route through the vent.

		IntegrationSupport.Direct( host, IntegrationSupport.ForceOpportunity( "hall" ) );
		var first = host.Step();

		// The forced decision carries the sweeper role and the ingress hint toward "hall".
		Assert.NotNull( first.Macro );
		Assert.Equal( "script_forced_opportunity", first.Macro.ReasonCode );
		Assert.Equal( PressureMode.Aggressive, first.Macro.Mode );
		Assert.Equal( "hall", first.Macro.CandidateRegionId );
		Assert.Contains( "sweeper", first.Macro.AllowedRoles );
		Assert.Contains( "vent1", first.Macro.IngressConstraints );

		// UseIngress is issued on the first tick; the host ack flips stage presence.
		var entry = Assert.Single( first.Actions );
		Assert.Equal( ActionKind.UseIngress, entry.Kind );
		Assert.Equal( "vent1", entry.IngressId );
		Assert.True( IntegrationSupport.HasCode( new[] { first }, "ingress_use" ) );
		var second = host.Step();
		Assert.Equal( StagePresence.Offstage, host.MonsterPresence );

		// Sweep: alternating node moves and seeded dwells until the 50s window ends, then exit.
		var sweep = new List<DecisionBatch> { second };
		sweep.AddRange( host.Run( 1060 ) );

		var moves = IntegrationSupport.AllActions( sweep ).FindAll( a => a.ReasonCode == "sweep_move" );
		Assert.True( moves.Count >= 2, "sweep must visit offstage nodes" );
		Assert.All( moves, a => Assert.Contains( a.NodeId, new[] { "on1", "on2" } ) );

		var dwells = IntegrationSupport.AllActions( sweep ).FindAll( a => a.ReasonCode == "sweep_dwell" );
		Assert.True( dwells.Count >= 1, "sweep must include dwell waits" );
		foreach ( var dwell in dwells )
		{
			double seconds = double.Parse( dwell.Param, System.Globalization.CultureInfo.InvariantCulture );
			Assert.InRange( seconds, 5.0, 40.0 ); // DEFAULT NodeDwell range
		}

		// The macro opportunity expires long before the sweep window ends (30s vs 50s);
		// the sweep survives on the micro side and exits through the vent afterwards.
		Assert.True( IntegrationSupport.HasCode( sweep, "opportunity_expired" ) );
		long entryTick = IntegrationSupport.FirstTickOfCode( new[] { first }, "ingress_use" );
		long firstMoveTick = IntegrationSupport.FirstTickOfCode( sweep, "sweep_move" );
		long exitTick = -1;
		for ( int i = sweep.Count - 1; i >= 0; i-- )
		{
			long t = IntegrationSupport.FirstTickOfCode( new[] { sweep[i] }, "ingress_use" );
			if ( t >= 0 ) { exitTick = t; break; }
		}
		long sweepEndTick = IntegrationSupport.FirstTickOfCode( sweep, "sweep_end" );
		Assert.True( entryTick >= 0 && firstMoveTick > entryTick );
		Assert.True( exitTick > firstMoveTick, "expected an exit UseIngress after the sweep window" );
		Assert.True( sweepEndTick > exitTick, "sweep_end is emitted when the exit ack lands" );
		Assert.Equal( StagePresence.Frontstage, host.MonsterPresence );
	}

	// 8 — dangerous aiming target in close range: threat display/hesitation, never a blind attack.
	[Fact]
	public void ThreatAwareHesitation()
	{
		var host = IntegrationSupport.NewHost();
		host.AddTarget( "t1", new Vec3( 6.0, 0.0, 0.0 ), "hall", threat: 0.9 );
		host.Targets[0].IsArmed = true;
		host.Targets[0].IsAimingAtMonster = true;
		host.Targets[0].IsVisible = true;

		var batches = new List<DecisionBatch>();
		for ( int i = 0; i < 40; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", host.Targets[0].Position, "hall" );
			batches.Add( host.Step() );
		}

		var threats = IntegrationSupport.AllActions( batches ).FindAll( a => a.Kind == ActionKind.Threat );
		Assert.True( threats.Count >= 1, "expected at least one threat display" );
		Assert.All( threats, a =>
		{
			Assert.Equal( "t1", a.TargetId );
			Assert.Equal( "hesitate", a.ReasonCode );
		} );
		Assert.True( IntegrationSupport.HasCode( batches, "hesitate" ) );
		// While the weapon stays aimed the hesitation re-fires after each 0.5s pause —
		// a threat-aware hold, never a blind commitment.

		// The threat is close (6m) but not very close (5m): the response holds — no attack,
		// no chase, and no flank (there is no ingress to take even though one roll happens).
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.Attack ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.Chase ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.UseIngress ) );
	}

	// 9 — weak visible target: chase, attack in range, then attack_cd blocks the re-commit.
	[Fact]
	public void ChaseAndAttack()
	{
		var host = IntegrationSupport.NewHost();
		var target = host.AddTarget( "t1", new Vec3( 5.0, 0.0, 0.0 ), "hall" );

		var batches = new List<DecisionBatch>();
		var attackTicks = new List<long>();
		for ( int i = 0; i < 80; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", target.Position, "hall" );
			var b = host.Step();
			batches.Add( b );
			if ( IntegrationSupport.HasCode( new[] { b }, "attack_commit" ) )
				attackTicks.Add( b.TickIndex );
		}

		Assert.True( attackTicks.Count >= 2, "expected a re-committed attack after the cooldown" );
		long chaseTick = IntegrationSupport.FirstTickOfCode( batches, "chase" );
		Assert.True( chaseTick >= 0 && chaseTick < attackTicks[0], "chase must precede the first attack" );

		// attack_cd is 1.5s = 30 ticks: no attack may be re-committed inside that window.
		long gap = attackTicks[1] - attackTicks[0];
		Assert.InRange( gap, 30, 32 );
		for ( long t = attackTicks[0] + 1; t < attackTicks[1]; t++ )
			Assert.Equal( -1, IntegrationSupport.FirstTickOfCode( new[] { batches[(int)t] }, "attack_commit" ) );
	}

	// 10 — heavy damage: the stun staggers first (priority 3), then the retreat fires (priority 4).
	[Fact]
	public void DamageDrivenRetreat()
	{
		var host = IntegrationSupport.NewHost();
		IntegrationSupport.AddPlanarNode( host, "n1", 20.0, 0.0, "atrium" );
		IntegrationSupport.AddPlanarNode( host, "n2", -20.0, 0.0, "atrium" );
		host.Run( 3 ); // warm up the health gauge so the drop is measured
		host.DamageMonster( 0.7 ); // health 1.0 → 0.3, below the 0.35 retreat line
		Assert.Equal( 1.0 - 0.7, host.MonsterHealth );

		var batches = host.Run( 40 );
		// The 0.7 drop charges the stun gauge to 1.4 ≥ 1: a stagger wait owns the first ticks.
		var first = batches[0];
		var stagger = Assert.Single( first.Actions );
		Assert.Equal( ActionKind.Wait, stagger.Kind );
		Assert.Equal( "stagger", stagger.ReasonCode );

		// Once the stagger timer ages out (~1.5s), the retreat motivation wins arbitration.
		long retreatTick = IntegrationSupport.FirstActionTick( batches, ActionKind.Retreat );
		Assert.True( retreatTick > 0, "expected a retreat action once the stagger expired" );
		Assert.InRange( retreatTick - first.TickIndex, 29, 35 );
		Assert.True( IntegrationSupport.HasCode( batches, "retreat_start" ) );
		var retreat = IntegrationSupport.AllActions( batches ).Find( a => a.Kind == ActionKind.Retreat );
		Assert.Equal( "n1", retreat.NodeId ); // farthest reachable node from the monster
		Assert.Contains( "retreat", host.System.MicroState.Motivations );

		// No retreat is emitted while the stagger still runs — arbitration order is honoured.
		Assert.Equal( -1, IntegrationSupport.FirstActionTick( new[] { batches[1], batches[2], batches[3] }, ActionKind.Retreat ) );
	}

	// 11 — unreachable nav: no node-addressed action goes there; 3 movement failures → recovery.
	[Fact]
	public void InaccessibleTarget()
	{
		var host = IntegrationSupport.NewHost();
		var target = host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		IntegrationSupport.AddPlanarNode( host, "h1", 10.0, 2.0, "hall", reachable: false );
		IntegrationSupport.AddPlanarNode( host, "h2", 14.0, 0.0, "hall", reachable: false );
		host.Policies[ActionKind.Chase] = ( request, h ) => ActionStatus.Failed; // navmesh says no

		var batches = new List<DecisionBatch>();
		for ( int i = 0; i < 130; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", target.Position, "hall" );
			batches.Add( host.Step() );
		}

		long recoveryTick = IntegrationSupport.FirstTickOfCode( batches, "nav_recovery" );
		Assert.True( recoveryTick > 0, "expected the nav recovery path after repeated failures" );

		// Exactly three failed chases lead to the recovery; the third failure's telemetry
		// shares the recovery tick's batch (acks are processed at the top of that tick).
		var beforeRecovery = batches.GetRange( 0, (int)recoveryTick );
		Assert.Equal( 3, IntegrationSupport.CountActions( beforeRecovery, ActionKind.Chase ) );
		var throughRecovery = batches.GetRange( 0, (int)recoveryTick + 1 );
		Assert.Equal( 3, IntegrationSupport.CountCode( throughRecovery, "action_failed" ) );

		var recoveryWait = IntegrationSupport.AllActions( batches ).Find( a => a.ReasonCode == "nav_recovery" );
		Assert.NotNull( recoveryWait );
		Assert.Equal( ActionKind.Wait, recoveryWait.Kind );
		Assert.Equal( "6", recoveryWait.Param ); // backoff = 2s × 3 consecutive failures
		Assert.Equal( 0, host.System.MicroState.ConsecutiveNavFailures ); // reset after the recovery issue

		// Backoff window: further chase attempts are refused as infeasible — no chase, no crash.
		var backoff = batches.GetRange( (int)recoveryTick + 1, 100 );
		Assert.Equal( 0, IntegrationSupport.CountActions( backoff, ActionKind.Chase ) );
		Assert.True( IntegrationSupport.CountCode( backoff, "action_infeasible" ) > 0 );

		// Unreachable nodes are never addressed: no search/stalk/ambush, no node-bound action.
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.Search ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.Stalk ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( batches, ActionKind.Ambush ) );
		Assert.DoesNotContain( IntegrationSupport.AllActions( batches ), a => a.NodeId == "h1" || a.NodeId == "h2" );
	}

	// 12 — host rejects MoveTo: rejection telemetry, failure counters, backoff, no crash.
	[Fact]
	public void HostRejection()
	{
		var host = IntegrationSupport.NewHost();
		IntegrationSupport.AddPlanarNode( host, "a1", 4.0, 2.0, "atrium" );
		IntegrationSupport.AddPlanarNode( host, "a2", 2.0, -2.0, "atrium" );
		host.Policies[ActionKind.MoveTo] = ( request, h ) => ActionStatus.Rejected;

		var batches = new List<DecisionBatch>();
		for ( int i = 0; i < 200; i++ )
		{
			// Fresh noises keep producing investigate-approach MoveTos to reject.
			if ( host.TickIndex == 0 ) host.EmitNoise( "noise-1", new Vec3( 4.0, 0.0, 0.0 ), "atrium", 0.8 );
			if ( host.TickIndex == 60 ) host.EmitNoise( "noise-2", new Vec3( 3.0, 0.0, 1.0 ), "atrium", 0.8 );
			if ( host.TickIndex == 80 ) host.EmitNoise( "noise-3", new Vec3( 5.0, 0.0, -1.0 ), "atrium", 0.8 );
			batches.Add( host.Step() );
		}

		// The third rejected approach is the third consecutive nav failure: recovery fires
		// in that same tick's batch (acks are processed at the top of the tick). Unactioned
		// noise memories keep re-triggering investigations afterwards, so later rejections
		// accumulate too — assert the causality, not the total.
		long recoveryTick = IntegrationSupport.FirstTickOfCode( batches, "nav_recovery" );
		Assert.True( recoveryTick > 0, "expected nav recovery after the third rejection" );
		var throughRecovery = batches.GetRange( 0, (int)recoveryTick + 1 );
		Assert.Equal( 3, IntegrationSupport.CountCode( throughRecovery, "action_rejected" ) );
		Assert.True( host.System.MicroState.LastNavFailureTick > 0 );
		var afterRecovery = batches.GetRange( (int)recoveryTick, batches.Count - (int)recoveryTick );
		Assert.Equal( 0, host.System.MicroState.ConsecutiveNavFailures );

		// Backoff behaviour: the next approach attempt is refused as infeasible instead of sent.
		Assert.True( IntegrationSupport.CountCode( afterRecovery, "action_infeasible" ) > 0 );

		// Alternative behaviour: rejected approaches do not wedge the stage machine —
		// investigations still complete and a search episode still ran.
		Assert.True( IntegrationSupport.CountCode( batches, "investigate_done" ) >= 2 );
		Assert.True( IntegrationSupport.CountCode( batches, "search_start" ) >= 1 );

		// The system never crashed and answered every tick.
		Assert.Equal( 200, host.History.Count );
		Assert.All( batches, b => Assert.True( b.Actions.Count <= 1 ) );
	}

	// 13 — ForceWithdrawal mid-chase: the retreat preempts the chase immediately.
	[Fact]
	public void ScriptedWithdrawal()
	{
		var host = IntegrationSupport.NewHost();
		var target = host.AddTarget( "t1", new Vec3( 12.0, 0.0, 0.0 ), "hall" );
		host.AddIngress( "vent1", -2.0, 0.0, 0.0, "atrium", "" );

		var batches = new List<DecisionBatch>();
		for ( int i = 0; i < 15; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", target.Position, "hall" );
			if ( host.TickIndex == 10 )
				IntegrationSupport.Direct( host, new ScriptDirective { Kind = ScriptDirectiveKind.ForceWithdrawal } );
			batches.Add( host.Step() );
		}

		// A chase was in flight when the directive landed.
		Assert.True( IntegrationSupport.FirstTickOfCode( batches, "chase" ) >= 0 );
		Assert.True( IntegrationSupport.FirstTickOfCode( batches, "chase" ) < 10 );

		var directiveTick = batches[10];
		Assert.True( IntegrationSupport.HasCode( new[] { directiveTick }, "script_withdrawal" ) );
		Assert.True( IntegrationSupport.HasCode( new[] { directiveTick }, "retreat_start" ) );
		Assert.True( IntegrationSupport.HasCode( new[] { directiveTick }, "preempt" ) ); // retreat outranks the awaited chase

		var retreat = Assert.Single( directiveTick.Actions );
		Assert.Equal( ActionKind.Retreat, retreat.Kind );
		Assert.Equal( "vent1", retreat.IngressId ); // withdraws toward the nearest usable ingress
		Assert.Equal( new Vec3( -2.0, 0.0, 0.0 ), retreat.Destination.Value );
		Assert.DoesNotContain( "forced_retreat", host.System.MicroState.Flags ); // consumed by acting on it
	}
}
