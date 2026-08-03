using System;

namespace SboxTwoBrains;

/// <summary>Thrown when a profile fails validation or inheritance resolution.</summary>
public sealed class ConfigException : Exception
{
	public ConfigException( string message ) : base( message ) { }
}
