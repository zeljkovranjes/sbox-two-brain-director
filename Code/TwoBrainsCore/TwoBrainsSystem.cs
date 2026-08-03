using System;
using System.Collections.Generic;
using TwoBrains.Core.Config;
using TwoBrains.Core.Contract;
using TwoBrains.Core.Determinism;
using TwoBrains.Core.Macro;
using TwoBrains.Core.Micro;
using TwoBrains.Core.Serialization;

namespace TwoBrains.Core;

/// <summary>
/// The two-brain system facade: owns the <see cref="PressureDirector"/> (macro) and
/// <see cref="MonsterAgent"/> (micro), the deterministic RNG forks, profile resolution and
/// the explicit per-tick order. This is the single entry point hosts drive.
///
/// Tick order (contract — see docs/TICK_ORDER.md):
///  1. Validate input (tick sequence, delta time, monster present).
///  2. Apply host acknowledgements (opportunity results → macro, action results → micro).
///  3. Evaluate script/profile changes (SetProfile handled here; others routed).
///  4. Update macro pressure (ages its timers; at most one opportunity transition).
///  5. Update micro perception + arbitration (ages memory/timers; declarative requests).
///  6. Resolve internal conflicts deterministically (unique ids, valid expiries).
///  7. Commit immutable state hash + telemetry batch.
///  8. Return decisions for later host acknowledgement.
///
/// Identical configuration, seed, ticks, snapshots and acknowledgements produce
/// byte-identical canonical output. Restore from <see cref="CaptureState"/> continues
/// exactly, including both RNG streams.
/// </summary>
public sealed class TwoBrainsSystem
{
	private const ulong MacroStreamId = 1;
	private const ulong MicroStreamId = 2;

	private readonly ProfileCatalogue _catalogue;
	private string _profileName;
	private string _modifierName;
	private EffectiveConfig _config;
	private readonly DeterministicRng _macroRng;
	private readonly DeterministicRng _microRng;
	private readonly PressureDirector _macro;
	private readonly MonsterAgent _micro;
	private long _nextTickIndex;
	private double _simTimeSeconds;

	/// <summary>Creates a system with a resolved, validated profile and a master seed.</summary>
	public TwoBrainsSystem( ProfileCatalogue catalogue, string profileName, ulong seed, string modifierName = null )
	{
		_catalogue = catalogue ?? throw new ArgumentNullException( nameof( catalogue ) );
		_profileName = profileName;
		_modifierName = modifierName;
		_config = modifierName == null ? catalogue.Resolve( profileName ) : catalogue.ResolveWithModifier( profileName, modifierName );
		_macroRng = DeterministicRng.Fork( seed, MacroStreamId );
		_microRng = DeterministicRng.Fork( seed, MicroStreamId );
		_macro = new PressureDirector( _macroRng );
		_micro = new MonsterAgent( _microRng );
		_nextTickIndex = 0;
		_simTimeSeconds = 0.0;
	}

	/// <summary>Final effective configuration currently in force.</summary>
	public EffectiveConfig Config => _config;

	/// <summary>Current macro state (read-only view for diagnostics).</summary>
	public PressureState MacroState => _macro.State;

	/// <summary>Current micro state (read-only view for diagnostics).</summary>
	public MicroState MicroState => _micro.State;

	/// <summary>Tick index the next <see cref="Tick"/> call must supply.</summary>
	public long NextTickIndex => _nextTickIndex;

	/// <summary>Accumulated simulated seconds.</summary>
	public double SimTimeSeconds => _simTimeSeconds;

	/// <summary>Active profile name (with modifier suffix when one is applied).</summary>
	public string ActiveProfileName => _modifierName == null ? _profileName : _profileName + "+" + _modifierName;

	/// <summary>Hot-switches the active profile (deterministic, validated at resolve time).</summary>
	public void SetProfile( string profileName, string modifierName = null )
	{
		_profileName = profileName;
		_modifierName = modifierName;
		_config = modifierName == null ? _catalogue.Resolve( profileName ) : _catalogue.ResolveWithModifier( profileName, modifierName );
	}

	/// <summary>Runs one tick. Snapshots must arrive in order, exactly once per tick index.</summary>
	public DecisionBatch Tick( WorldSnapshot snapshot )
	{
		if ( snapshot == null ) throw new ArgumentNullException( nameof( snapshot ) );
		if ( snapshot.Monster == null ) throw new ArgumentException( "Snapshot requires a Monster." );
		if ( snapshot.TickIndex != _nextTickIndex )
			throw new InvalidOperationException( string.Format( System.Globalization.CultureInfo.InvariantCulture,
				"Tick order violation: expected tick {0}, got {1}.", _nextTickIndex, snapshot.TickIndex ) );

		var ctx = new TickContext( snapshot.TickIndex, snapshot.DeltaTimeSeconds );
		var telemetry = new List<TelemetryEvent>();

		// 2. acknowledgements
		var macroAcks = new List<ActionResult>();
		var microAcks = new List<ActionResult>();
		foreach ( var ack in snapshot.Acknowledgements )
		{
			if ( ack.ActionId == _macro.State.PendingOpportunityId && ack.ActionId.Length > 0 )
				macroAcks.Add( ack );
			else
				microAcks.Add( ack );
		}
		_macro.ApplyOpportunityResults( ctx, _config, macroAcks, telemetry );
		_micro.ApplyActionResults( ctx, _config, microAcks, telemetry );

		// 3. script/profile changes
		var macroDirectives = new List<ScriptDirective>();
		var microDirectives = new List<ScriptDirective>();
		foreach ( var d in snapshot.Directives )
		{
			switch ( d.Kind )
			{
				case ScriptDirectiveKind.SetProfile:
					if ( !_catalogue.Contains( d.ProfileName ) )
						throw new ConfigException( "SetProfile directive references unknown profile '" + d.ProfileName + "'." );
					SetProfile( d.ProfileName, _modifierName );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "config", "profile_switch", "profile=" + d.ProfileName ) );
					break;
				case ScriptDirectiveKind.SetPressureMode:
				case ScriptDirectiveKind.SetProgression:
				case ScriptDirectiveKind.ResetPressure:
				case ScriptDirectiveKind.ForceOpportunity:
					macroDirectives.Add( d );
					break;
				case ScriptDirectiveKind.ForceWithdrawal:
				case ScriptDirectiveKind.PlayScriptedSequence:
				case ScriptDirectiveKind.Despawn:
					microDirectives.Add( d );
					break;
			}
		}
		_macro.ApplyDirectives( ctx, _config, macroDirectives, telemetry );
		_micro.ApplyDirectives( ctx, _config, microDirectives, telemetry );

		// 4. macro
		var macroDecision = _macro.Tick( ctx, snapshot, _config, telemetry );

		// 5. micro
		var actions = _micro.Tick( ctx, snapshot, macroDecision, _config, telemetry ) ?? new List<ActionRequest>();

		// 6. deterministic conflict resolution / validation
		ValidateActions( ctx, actions );

		// 7. commit
		_simTimeSeconds += ctx.DeltaTimeSeconds;
		_nextTickIndex = ctx.TickIndex + 1;

		var batch = new DecisionBatch
		{
			TickIndex = ctx.TickIndex,
			Macro = macroDecision,
			Actions = actions,
			Telemetry = telemetry,
			StateHash = ComputeStateHash(),
		};
		return batch;
	}

	private static void ValidateActions( TickContext ctx, List<ActionRequest> actions )
	{
		var seen = new HashSet<string>( StringComparer.Ordinal );
		foreach ( var action in actions )
		{
			if ( string.IsNullOrEmpty( action.ActionId ) )
				throw new InvalidOperationException( "Action request missing ActionId at tick " + ctx.TickIndex + "." );
			if ( !seen.Add( action.ActionId ) )
				throw new InvalidOperationException( "Duplicate ActionId '" + action.ActionId + "' at tick " + ctx.TickIndex + "." );
			if ( action.ExpiryTick <= ctx.TickIndex )
				throw new InvalidOperationException( "Action '" + action.ActionId + "' has non-future ExpiryTick at tick " + ctx.TickIndex + "." );
		}
	}

	private ulong ComputeStateHash()
	{
		var macroState = _macro.CaptureState();
		var microState = _micro.CaptureState();
		var (m0, m1) = _macroRng.GetState();
		var (u0, u1) = _microRng.GetState();
		return CanonicalJson.Hash( string.Concat(
			_nextTickIndex.ToString( System.Globalization.CultureInfo.InvariantCulture ), "|",
			_simTimeSeconds.ToString( "R", System.Globalization.CultureInfo.InvariantCulture ), "|",
			m0.ToString( "X16" ), m1.ToString( "X16" ), u0.ToString( "X16" ), u1.ToString( "X16" ), "|",
			macroState, "|", microState ) );
	}

	/// <summary>Captures the complete deterministic state (schema version 1).</summary>
	public SavedStateEnvelope CaptureState()
	{
		var (m0, m1) = _macroRng.GetState();
		var (u0, u1) = _microRng.GetState();
		return new SavedStateEnvelope
		{
			SchemaVersion = 1,
			ConfigVersion = _config.ConfigVersion,
			TickIndex = _nextTickIndex - 1,
			SimTimeSeconds = _simTimeSeconds,
			MacroRngS0 = m0,
			MacroRngS1 = m1,
			MicroRngS0 = u0,
			MicroRngS1 = u1,
			MacroStateJson = _macro.CaptureState(),
			MicroStateJson = _micro.CaptureState(),
		};
	}

	/// <summary>Restores a state previously captured by <see cref="CaptureState"/>.</summary>
	public void RestoreState( SavedStateEnvelope envelope )
	{
		if ( envelope == null ) throw new ArgumentNullException( nameof( envelope ) );
		if ( envelope.SchemaVersion != 1 )
			throw new ConfigException( "Unsupported saved-state schema version " + envelope.SchemaVersion + " (expected 1)." );
		_macroRng.SetState( envelope.MacroRngS0, envelope.MacroRngS1 );
		_microRng.SetState( envelope.MicroRngS0, envelope.MicroRngS1 );
		_macro.RestoreState( envelope.MacroStateJson );
		_micro.RestoreState( envelope.MicroStateJson );
		_nextTickIndex = envelope.TickIndex + 1;
		_simTimeSeconds = envelope.SimTimeSeconds;
	}
}
