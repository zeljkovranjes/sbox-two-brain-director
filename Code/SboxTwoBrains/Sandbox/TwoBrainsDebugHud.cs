using System;
using System.Linq;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using SboxTwoBrains;

namespace SboxTwoBrains.Host;

/// <summary>
/// Compact top-left live telemetry overlay for the two-brain director, mirroring the
/// adaptive-director demo HUD design. Attach through <see cref="TwoBrainsDebugHudSpawner"/>
/// (or manually next to a ScreenPanel). Press F to toggle the advanced block.
/// </summary>
[Title( "Two-Brain Debug HUD" )]
[Category( "AI" )]
public sealed class TwoBrainsDebugHud : PanelComponent
{
	private Label _title;
	private Label _telemetry;
	private Label _modeValue;
	private Label _advancedTelemetry;
	private Label _controlsHint;
	private TwoBrainsComponent _director;
	private PressureDecision _lastMacro;
	private bool _showAdvanced;

	protected override void OnStart()
	{
		Panel.StyleSheet.Load( "/SboxTwoBrains/Sandbox/TwoBrainsDebugHud.cs.scss" );
		_title = Panel.Add.Label( "TWO-BRAIN DIRECTOR", "twobrains-title" );
		_telemetry = Panel.Add.Label( "STATUS       STARTING", "twobrains-telemetry basic" );
		_modeValue = Panel.Add.Label( "STARTING", "twobrains-mode-value mode-initializing" );
		_advancedTelemetry = Panel.Add.Label( "", "twobrains-telemetry advanced" );
		_controlsHint = Panel.Add.Label( "F FOR ADVANCED DIRECTOR TELEMETRY", "twobrains-controls" );
	}

	protected override void OnUpdate()
	{
		if ( Input.Keyboard.Pressed( "f" ) )
		{
			_showAdvanced = !_showAdvanced;
			_advancedTelemetry.SetClass( "visible", _showAdvanced );
		}

		if ( _director is null || !_director.IsValid )
		{
			_director = Scene.GetAllComponents<TwoBrainsComponent>().FirstOrDefault();
			_lastMacro = null;
		}

		if ( _director is null )
		{
			UpdateModeValue( "OFFLINE", "mode-initializing" );
			_telemetry.Text = "STATUS       OFFLINE";
			_advancedTelemetry.Text = "TWO-BRAINS COMPONENT NOT FOUND IN SCENE";
			return;
		}

		var system = _director.System;
		if ( system is null )
		{
			UpdateModeValue( "STARTING", "mode-initializing" );
			_telemetry.Text = "STATUS       STARTING";
			_advancedTelemetry.Text = "CORE SYSTEM NOT INITIALIZED";
			return;
		}

		if ( !_director.DebugEnabled )
		{
			UpdateModeValue( "DISABLED", "mode-initializing" );
			_telemetry.Text = "STATUS       DISABLED (DebugEnabled OFF)";
			_advancedTelemetry.Text = "ENABLE DebugEnabled ON THE TWO-BRAIN COMPONENT FOR TELEMETRY";
			return;
		}

		var batch = _director.LastBatch;
		if ( batch?.Macro is not null )
			_lastMacro = batch.Macro;

		var macro = system.MacroState;
		var micro = system.MicroState;
		var aggressive = macro.Mode == PressureMode.Aggressive;
		var pressure = Clamp01( macro.Progression );
		var urgency = Clamp01( _lastMacro?.Urgency ?? 0.0 );
		var maxOpportunities = system.Config.Pressure.MaxOpportunities;

		UpdateModeValue( aggressive ? "AGGRESSIVE" : "NORMAL", aggressive ? "mode-aggressive" : "mode-normal" );

		_telemetry.Text =
			$"STATUS       ONLINE\n" +
			$"MODE\n" +
			$"PRESSURE     {pressure:0.000} {BuildMeter( (float)pressure )}\n" +
			$"URGENCY      {urgency:0.000} {BuildMeter( (float)urgency )}\n" +
			$"OPPORTUNITIES {macro.CompletedOpportunities}/{maxOpportunities}\n" +
			$"PENDING      {micro.PendingActions.Count}" +
			(_showAdvanced ? "" : "\n\n[+] Press F to show advanced settings");

		_advancedTelemetry.Text =
			BuildAdvancedTelemetry( system, macro, micro, batch ) +
			"\n\n[-] Press F to hide advanced settings";
	}

	private void UpdateModeValue( string text, string activeClass )
	{
		_modeValue.SetClass( "mode-initializing", activeClass == "mode-initializing" );
		_modeValue.SetClass( "mode-normal", activeClass == "mode-normal" );
		_modeValue.SetClass( "mode-aggressive", activeClass == "mode-aggressive" );
		_modeValue.Text = text;
	}

	private string BuildAdvancedTelemetry( TwoBrainsSystem system, PressureState macro, MicroState micro, DecisionBatch batch )
	{
		var tickRate = Math.Max( 1, _director.TicksPerSecond );
		var lastTick = system.NextTickIndex - 1;

		var roles = _lastMacro?.AllowedRoles is { Length: > 0 } r ? string.Join( " ", r ) : "NONE";
		var motivations = micro.Motivations.Count > 0 ? string.Join( " ", micro.Motivations ) : "NONE";

		var strongest = micro.Memories.OrderByDescending( m => m.DecayedConfidence ).FirstOrDefault();
		var memoryDetail = strongest is null
			? "NONE"
			: $"STRONGEST {strongest.Channel} {strongest.DecayedConfidence:0.00} {TickAge( strongest.LastConfirmedTick, lastTick, tickRate ):0.0}s";

		var target = string.IsNullOrEmpty( micro.CurrentTargetId ) ? "NONE" : micro.CurrentTargetId;
		var targetAge = micro.LastSensedTargetTick < 0
			? "NEVER"
			: $"{TickAge( micro.LastSensedTargetTick, lastTick, tickRate ):0.0}s";

		micro.Gauges.TryGetValue( StateKeys.Retreat, out var retreat );
		micro.Gauges.TryGetValue( StateKeys.Stun, out var stun );

		var telemetryLines = "NONE";
		if ( batch?.Telemetry is { Count: > 0 } telemetry )
		{
			telemetryLines = string.Join( "\n", telemetry
				.Skip( Math.Max( 0, telemetry.Count - 3 ) )
				.Select( e => $"  {e.Code} {Truncate( e.Message, 70 )}" ) );
		}

		return
			$"\nPROGRESSION  {macro.Progression:0.000}\n" +
			$"COOLDOWN     {Math.Max( 0.0, macro.CooldownRemaining ):0.0}s\n" +
			$"CANDIDATE    {(macro.ActiveCandidateId.Length > 0 ? macro.ActiveCandidateId : "NONE")}\n" +
			$"ROLES        {roles}\n" +
			$"MODULE       {(micro.ActiveModule.Length > 0 ? micro.ActiveModule : "NONE")}\n" +
			$"MOTIVATIONS  {motivations}\n" +
			$"MEMORIES     {micro.Memories.Count}  {memoryDetail}\n" +
			$"TARGET       {target}  LAST SENSED {targetAge}\n" +
			$"NAV FAILURES {micro.ConsecutiveNavFailures}\n" +
			$"AWAITING     {(micro.AwaitingActionId.Length > 0 ? micro.AwaitingActionId : "NONE")}\n" +
			$"RETREAT      {retreat:0.000} {BuildMeter( (float)Clamp01( retreat ) )}\n" +
			$"STUN         {stun:0.000} {BuildMeter( (float)Clamp01( stun ) )}\n" +
			$"PROFILE      {system.ActiveProfileName}\n" +
			$"SEED/TICK    {_director.Seed} / {system.NextTickIndex} @ {system.SimTimeSeconds:0.00}s\n" +
			$"STATE HASH   {(batch?.StateHash ?? 0UL):X16}\n" +
			$"LAST TELEMETRY\n{telemetryLines}";
	}

	private static double TickAge( long referenceTick, long lastTick, int tickRate )
	{
		return Math.Max( 0.0, (lastTick - referenceTick) / (double)tickRate );
	}

	private static string Truncate( string text, int max )
	{
		if ( string.IsNullOrEmpty( text ) || text.Length <= max )
			return text ?? "";
		return text.Substring( 0, max - 1 ) + "~";
	}

	private static double Clamp01( double value )
	{
		return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
	}

	private static string BuildMeter( float value, int width = 20 )
	{
		var filled = (int)MathF.Round( Math.Clamp( value, 0.0f, 1.0f ) * width );
		return $"|{new string( '-', filled )}{new string( '.', width - filled )}|";
	}
}
