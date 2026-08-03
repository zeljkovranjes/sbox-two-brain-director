using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Gauge fill/decrease formula and timing tests (research rows 2-3).</summary>
public sealed class GaugeFormulaTests
{
	[Fact]
	public void Fill_MatchesResearchFormulaExactly_Over120Ticks()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.FillSeconds = 2.0;
		cfg.Pressure.AggressiveThresholdProgression = 1.0; // asymptotic fill never crosses 1.0
		var director = new PressureDirector( new DeterministicRng( 7 ) );
		var telemetry = new List<TelemetryEvent>();

		const double dt = 1.0 / 60.0;
		double expected = 0.0;
		for ( long tick = 0; tick < 120; tick++ )
		{
			var decision = MacroDrive.TickOne( director, cfg,
				WorldBuilder.At( tick, dt ).Target( "t", "atrium", 10.0, 0.0 ), telemetry );
			expected = expected + ( 1.0 - expected ) / 2.0 * dt;
			Assert.Null( decision );
			Assert.Equal( expected, director.State.Progression );
		}
		Assert.True( director.State.Progression > 0.6 && director.State.Progression < 0.64 );
	}

	[Fact]
	public void Decrease_WaitsForDelay_ThenDecreasesLinearly()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.DecreaseDelaySeconds = 0.5;
		var director = new PressureDirector( new DeterministicRng( 8 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( 0.4375, director.State.Progression );

		// Latch lost at tick 2: delay is (re)armed and decrease may not start yet.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 2, 0.25 ), telemetry );
		Assert.False( director.State.CandidateLatched );
		Assert.Equal( 0.5, director.State.DecreaseDelayRemaining );
		Assert.Equal( 0.4375, director.State.Progression );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 ), telemetry );
		Assert.Equal( 0.25, director.State.DecreaseDelayRemaining );
		Assert.Equal( 0.4375, director.State.Progression );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 ), telemetry );
		Assert.Equal( 0.0, director.State.DecreaseDelayRemaining );
		Assert.Equal( 0.1875, director.State.Progression );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 5, 0.25 ), telemetry );
		Assert.Equal( 0.0, director.State.Progression );
	}

	[Fact]
	public void Fill_ClampsAtOne()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.FillSeconds = 0.5;
		cfg.Pressure.AggressiveThresholdProgression = 1.0;
		var director = new PressureDirector( new DeterministicRng( 9 ) );
		var telemetry = new List<TelemetryEvent>();

		// dt=2s with fill=0.5s would overshoot (increment 4x the gap); clamp holds it at 1.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 2.0 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( 1.0, director.State.Progression );
	}

	[Fact]
	public void Decrease_FloorsAtZero()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 10 ) );
		var telemetry = new List<TelemetryEvent>();

		// Latch and fill a little, then lose the target and set a small progression directly.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( 0.25, director.State.Progression );
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 0.1 } ), telemetry );
		// Decrease of 0.25/tick from 0.1 must floor at 0, not go negative.
		Assert.Equal( 0.0, director.State.Progression );
	}

	[Fact]
	public void Gauge_DoesNotFill_DuringCooldown()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.CooldownSeconds = 0.5;
		var director = new PressureDirector( new DeterministicRng( 11 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 4, telemetry );
		// Tick 3 completed the opportunity: progression = 1.0 (start), cooldown = 0.5.
		Assert.Equal( 1.0, director.State.Progression );
		Assert.Equal( 0.5, director.State.CooldownRemaining );

		// Tick 4: cooldown ages to 0.25; fill is suppressed while cooling down.
		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Null( decision );
		Assert.Equal( 1.0, director.State.Progression );
		Assert.Equal( 0.25, director.State.CooldownRemaining );
	}

	[Fact]
	public void Gauge_Frozen_WhileAggressive()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.SweepDurationSeconds = 0.5; // two ticks of aggressive window
		var director = new PressureDirector( new DeterministicRng( 12 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 4, telemetry );
		Assert.Equal( "mode_aggressive_start", decisions[2].ReasonCode );
		Assert.Equal( 0.578125, decisions[2].Progression );

		// Tick 3: mid-sweep; gauge must not move and no completion yet.
		Assert.Null( decisions[3] );
		Assert.Equal( 0.578125, director.State.Progression );
		Assert.Equal( 0.25, director.State.SweepSecondsRemaining );
	}

	[Fact]
	public void NoFill_WithoutCandidate()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 13 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ), 0, 10, telemetry );
		Assert.All( decisions, d => Assert.Null( d ) );
		Assert.Equal( 0.0, director.State.Progression );
		Assert.Empty( telemetry );
	}
}
