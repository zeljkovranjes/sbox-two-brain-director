using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 7 — attack pipeline. Precondition: current target evidence on the
/// Visual/Touch/Damage channels (or omniscience), a valid living target within
/// ChaseGiveUpDistance, no attack cooldown/ban and host attack capability. Core: commit an
/// Attack inside AttackRange (starts the attack cooldown), otherwise Chase the last sensed
/// position at fastest speed. An ongoing chase survives on memory alone for
/// ChaseGiveUpSeconds, then the target is dropped with a chase_lost reason.
/// </summary>
internal sealed class AttackModule : IAgentModule
{
	public string Name => "Attack";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		string tid = s.CurrentTargetId;
		if ( tid.Length == 0 ) return ModuleResult.Ineligible();
		var target = ac.FindTarget( tid );
		if ( target == null || !target.IsValid || !target.IsAlive ) return ModuleResult.Ineligible();
		if ( !ac.Monster.CanAttack ) return ModuleResult.Ineligible();
		if ( ac.TimerActive( StateKeys.AttackCooldown ) || ac.TimerActive( StateKeys.AttackBan ) ) return ModuleResult.Ineligible();

		var c = ac.Cfg.Combat;
		var best = ac.BestTarget();
		bool current = best != null && best.TargetId == tid
			&& ( best.Source == EvidenceSource.Omniscient
				|| best.Source == EvidenceSource.CurrentVisual
				|| ( best.Source == EvidenceSource.CurrentOther && ( best.Channel == SenseChannel.Touch || best.Channel == SenseChannel.Damage ) ) );

		if ( current )
		{
			double dist = ac.RouteOrPlanar( best.Position, best.RegionId );
			if ( dist > c.ChaseGiveUpDistance ) return ModuleResult.Ineligible();
			if ( !ac.MayEmit ) return ModuleResult.Running();
			if ( dist <= c.AttackRange )
			{
				s.Flags.Remove( StateKeys.Chasing );
				s.Timers[StateKeys.AttackCooldown] = c.AttackCooldownSeconds;
				var attack = ac.Draft( ActionKind.Attack, ReasonCodes.AttackCommit );
				attack.TargetId = tid;
				attack.Destination = best.Position;
				attack.SpeedScale = 1.0;
				return ModuleResult.Act( attack, c.AttackCooldownSeconds + 2.0 );
			}
			s.Flags.Add( StateKeys.Chasing );
			var chase = ac.Draft( ActionKind.Chase, ReasonCodes.Chase );
			chase.TargetId = tid;
			chase.Destination = best.Position;
			chase.SpeedScale = ac.Cfg.Movement.SpeedFastest;
			return ModuleResult.Act( chase, ac.MovementTimeout( best.Position, best.RegionId ) );
		}

		// chase continuation on remembered evidence only
		if ( s.Flags.Contains( StateKeys.Chasing ) && s.LastSensedTargetPosition.HasValue && s.LastSensedTargetTick >= 0 )
		{
			double elapsed = ( ac.TickIndex - s.LastSensedTargetTick ) * ac.Dt;
			if ( elapsed > c.ChaseGiveUpSeconds )
			{
				s.CurrentTargetId = "";
				s.Flags.Remove( StateKeys.Chasing );
				s.Flags.Remove( StateKeys.SuspectResponded );
				ac.Emit( ReasonCodes.ChaseLost, "target=" + tid );
				return ModuleResult.Ineligible();
			}
			if ( !ac.MayEmit ) return ModuleResult.Running();
			var pos = s.LastSensedTargetPosition.Value;
			var chase = ac.Draft( ActionKind.Chase, ReasonCodes.Chase );
			chase.TargetId = tid;
			chase.Destination = pos;
			chase.SpeedScale = ac.Cfg.Movement.SpeedFastest;
			return ModuleResult.Act( chase, ac.MovementTimeout( pos, target.RegionId ) );
		}
		return ModuleResult.Ineligible();
	}
}
