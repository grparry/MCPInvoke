using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration; // Added for IConfiguration

namespace MCPInvoke.Testing;

/// <summary>
/// Provides sample tool methods for testing the MCPInvoke functionality.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SampleToolService : ControllerBase
{
    private readonly IConfiguration? _configuration; // For GetConfigurationValue

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleToolService"/> class.
    /// </summary>
    /// <param name="configuration">Optional configuration to use for the service.</param>
    public SampleToolService(IConfiguration? configuration = null)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Adds two integers.
    /// </summary>
    /// <param name="a">The first integer.</param>
    /// <param name="b">The second integer.</param>
    /// <returns>The sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
    [HttpPost("Add")] // Or [HttpGet("Add")] if preferred
    public int Add(int a, int b)
    {
        return a + b;
    }

    /// <summary>
    /// Subtracts the second integer from the first.
    /// </summary>
    /// <param name="a">The first integer (minuend).</param>
    /// <param name="b">The second integer (subtrahend).</param>
    /// <returns>The result of subtracting <paramref name="b"/> from <paramref name="a"/>.</returns>
    [HttpPost("Subtract")] // Or [HttpGet("Subtract")]
    public int Subtract(int a, int b)
    {
        return a - b;
    }

    /// <summary>
    /// Greets a person with an optional title.
    /// </summary>
    /// <param name="name">The name of the person to greet.</param>
    /// <param name="title">An optional title for the person (e.g., Mr., Dr.).</param>
    /// <returns>A greeting string.</returns>
    [HttpGet("Greet")]
    public string Greet(string name, string? title = null)
    {
        return $"Hello, {(string.IsNullOrEmpty(title) ? "" : title + " ")}{name}!";
    }

    /// <summary>
    /// Gets the current server time, optionally including milliseconds.
    /// </summary>
    /// <param name="includeMilliseconds">A boolean indicating whether to include milliseconds in the time string.</param>
    /// <returns>A string representing the current server time.</returns>
    [HttpGet("GetServerTime")]
    public async Task<string> GetServerTimeAsync(bool includeMilliseconds = false)
    {
        await Task.Delay(50); // Simulate some async work
        string format = includeMilliseconds ? "HH:mm:ss.fff" : "HH:mm:ss";
        return DateTime.Now.ToString(format);
    }

    /// <summary>
    /// Gets the sample version of the tool service.
    /// </summary>
    /// <returns>A string representing the version.</returns>
    [HttpGet("GetVersion")]
    public string GetVersion() // Changed from static to instance method
    {
        return "1.0-sample";
    }

    /// <summary>
    /// Gets a configuration value from the application settings.
    /// This requires IConfiguration to be injected or passed in.
    /// </summary>
    /// <param name="key">The key of the configuration value to retrieve.</param>
    /// <returns>The configuration value, or null if not found or _configuration is null.</returns>
    [HttpGet("GetConfigurationValue")]
    public string? GetConfigurationValue(string key)
    {
        return _configuration?.GetValue<string>(key);
    }

    /// <summary>
    /// A method designed to throw an exception for testing error handling capabilities.
    /// </summary>
    /// <param name="message">A message to include in the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Always thrown to simulate a server-side error, including the provided <paramref name="message"/>.</exception>
    [HttpPost("TestError")]
    public void TestError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentNullException(nameof(message), "Message cannot be null or empty for TestError.");
        }
        throw new InvalidOperationException($"TestError invoked with: {message}");
    }
}
