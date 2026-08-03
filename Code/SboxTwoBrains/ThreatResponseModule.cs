using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 5 — threat-aware response to a dangerous close target. Eligible on current
/// visual evidence of a target at or above the dangerous threat rating within CloseDistance
/// (route distance when available, else planar), kept alive briefly by visual retention.
/// Behaviour: hesitate when a weapon is aimed, one flank roll per episode (seeded RNG via
/// ingress toward the target region), fall through to Attack when the target is very close,
/// otherwise hold. Episodes are bounded by the threat timeout.
/// </summary>
internal sealed class ThreatResponseModule : IAgentModule
{
	public string Name => "ThreatResponse";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		var cfg = ac.Cfg.Threat;

		TargetSnapshot threat = null;
		double threatDist = 0.0;
		foreach ( var stim in ac.World.CurrentStimuli )
		{
			if ( stim == null || stim.Channel != SenseChannel.Visual || string.IsNullOrEmpty( stim.TargetId ) ) continue;
			var t = ac.FindTarget( stim.TargetId );
			if ( t == null || !t.IsValid || !t.IsAlive || t.ThreatRating < cfg.DangerousThreatRating ) continue;
			double d = ac.RouteOrPlanar( stim.Position, !string.IsNullOrEmpty( stim.RegionId ) ? stim.RegionId : t.RegionId );
			if ( d > cfg.CloseDistance ) continue;
			if ( threat == null || d < threatDist || ( d == threatDist && string.CompareOrdinal( t.TargetId, threat.TargetId ) < 0 ) )
			{
				threat = t;
				threatDist = d;
			}
		}
		if ( threat == null && s.CurrentTargetId.Length > 0 && s.LastSensedTargetTick >= 0 && s.LastSensedTargetPosition.HasValue
			&& ( ac.TickIndex - s.LastSensedTargetTick ) * ac.Dt <= cfg.VisualRetentionSeconds )
		{
			// visual retention: stay eligible briefly after losing sight of the threat
			var t = ac.FindTarget( s.CurrentTargetId );
			if ( t != null && t.IsValid && t.IsAlive && t.ThreatRating >= cfg.DangerousThreatRating )
			{
				double d = ac.RouteOrPlanar( s.LastSensedTargetPosition.Value, t.RegionId );
				if ( d <= cfg.CloseDistance )
				{
					threat = t;
					threatDist = d;
				}
			}
		}

		bool episode = s.Flags.Contains( StateKeys.ThreatEpisodeActive );
		if ( threat == null )
		{
			if ( episode && !ac.TimerActive( StateKeys.ThreatEpisode ) )
				EndEpisode( ac );
			return ModuleResult.Ineligible();
		}
		if ( !episode )
		{
			s.Flags.Add( StateKeys.ThreatEpisodeActive );
			s.Flags.Remove( StateKeys.FlankRolled );
			s.Timers[StateKeys.ThreatEpisode] = cfg.ThreatTimeoutSeconds;
		}
		else if ( !ac.TimerActive( StateKeys.ThreatEpisode ) )
		{
			EndEpisode( ac );
			return ModuleResult.Ineligible();
		}

		if ( !ac.MayEmit ) return ModuleResult.Running();

		if ( threat.IsAimingAtMonster && !ac.TimerActive( StateKeys.Hesitate ) )
		{
			s.Timers[StateKeys.Hesitate] = cfg.AimedWeaponHesitationSeconds;
			var draft = ac.Draft( ActionKind.Threat, ReasonCodes.Hesitate );
			draft.TargetId = threat.TargetId;
			return ModuleResult.Act( draft, cfg.AimedWeaponHesitationSeconds + 1.0 );
		}
		if ( !s.Flags.Contains( StateKeys.FlankRolled ) )
		{
			s.Flags.Add( StateKeys.FlankRolled );
			if ( ac.Monster.CanTraverseIngress && ac.Rng.NextChance( cfg.FlankChance ) )
			{
				var ing = ac.IngressToward( threat.RegionId );
				if ( ing != null )
				{
					var draft = ac.Draft( ActionKind.UseIngress, ReasonCodes.Flank );
					draft.IngressId = ing.IngressId;
					draft.Destination = ing.Position;
					draft.TargetId = threat.TargetId;
					return ModuleResult.Act( draft, ac.Cfg.Offstage.IngressTimeoutSeconds );
				}
			}
		}
		if ( threatDist <= cfg.VeryCloseDistance )
			return ModuleResult.Ineligible(); // fall through to Attack
		return ModuleResult.Running(); // threat-aware hold
	}

	private static void EndEpisode( AgentContext ac )
	{
		ac.State.Flags.Remove( StateKeys.ThreatEpisodeActive );
		ac.State.Flags.Remove( StateKeys.FlankRolled );
		ac.State.Motivations.Remove( "threat" );
		ac.Emit( ReasonCodes.ThreatTimeout, "threat episode expired" );
	}
}
