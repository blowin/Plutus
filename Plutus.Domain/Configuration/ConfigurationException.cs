namespace Plutus.Domain.Configuration;

/// <summary>A configuration value that cannot be used as stated, reported as a failure rather than thrown at a user.</summary>
public sealed class ConfigurationException(string message) : Exception(message);
