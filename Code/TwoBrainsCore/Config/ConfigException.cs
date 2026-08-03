using System;

namespace TwoBrains.Core.Config;

/// <summary>Thrown when a profile fails validation or inheritance resolution.</summary>
public sealed class ConfigException : Exception
{
	public ConfigException( string message ) : base( message ) { }
}
