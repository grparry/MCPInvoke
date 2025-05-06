using System.Collections.Generic;

namespace MCPInvoke.AspNetCore
{
    /// <summary>
    /// Configuration options for <see cref="AspNetControllerToolDefinitionProvider"/>.
    /// </summary>
    public class AspNetControllerToolOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to include the controller name as part of the tool name.
        /// When true, tool names will be in the format "{ControllerName}_{ActionName}".
        /// When false, tool names will just be the action method names.
        /// </summary>
        public bool IncludeControllerNameInToolName { get; set; } = true;

        /// <summary>
        /// Gets or sets a collection of controller names to exclude from MCP tool discovery.
        /// The "Controller" suffix is not required (e.g., use "Home" to exclude "HomeController").
        /// </summary>
        public ICollection<string> ExcludedControllers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether to expose model binding for complex types.
        /// When true, complex parameter types will be exposed with their properties as sub-parameters.
        /// When false, complex types will just be represented as "object" type parameters.
        /// </summary>
        public bool ExposeComplexTypeProperties { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to skip ASP.NET Core infrastructure parameters.
        /// These include parameters like HttpContext, CancellationToken, etc.
        /// </summary>
        public bool SkipInfrastructureParameters { get; set; } = true;
    }
}
