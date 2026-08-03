using System.Linq;
using Sandbox;

namespace SboxTwoBrains.Host;

/// <summary>
/// Self-contained attach story for the debug overlay: drop this one component on any
/// GameObject and it ensures a child ScreenPanel GameObject exists carrying a
/// <see cref="TwoBrainsDebugHud"/>. Nothing else to wire up.
/// </summary>
[Title( "Two-Brain Debug HUD Spawner" )]
[Category( "AI" )]
public sealed class TwoBrainsDebugHudSpawner : Component
{
	private const string HudObjectName = "TwoBrainsDebugHud";

	protected override void OnStart()
	{
		var panelObject = GameObject.Children.FirstOrDefault( child => child.Name == HudObjectName );
		if ( panelObject is null )
		{
			panelObject = Scene.CreateObject( false );
			panelObject.Name = HudObjectName;
			panelObject.SetParent( GameObject );
		}

		panelObject.Components.GetOrCreate<ScreenPanel>();
		var hud = panelObject.Components.GetOrCreate<TwoBrainsDebugHud>();
		hud.Enabled = true;
		panelObject.Enabled = true;
	}
}
