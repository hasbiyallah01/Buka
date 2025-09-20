using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace AmalaSpotLocator.Configuration;

public class SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal server error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("An error occurred while processing the request") }
                            }
                        }
                    }
                }
            });
        }

        if (IsRateLimitedEndpoint(context))
        {
            operation.Responses.TryAdd("429", new OpenApiResponse
            {
                Description = "Rate limit exceeded",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Rate limit exceeded. Try again in 60 seconds.") },
                                ["retryAfter"] = new OpenApiSchema { Type = "integer", Example = new Microsoft.OpenApi.Any.OpenApiInteger(60) }
                            }
                        }
                    }
                }
            });
        }

        if (RequiresAuthentication(context))
        {
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Unauthorized - Invalid or missing authentication token",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Unauthorized access") }
                            }
                        }
                    }
                }
            });
        }

        if (RequiresAuthorization(context))
        {
            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "Forbidden - Insufficient permissions",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Insufficient permissions") }
                            }
                        }
                    }
                }
            });
        }

        var controllerName = GetControllerName(context);
        if (!string.IsNullOrEmpty(controllerName))
        {
            operation.Tags = new List<OpenApiTag> { new OpenApiTag { Name = controllerName } };
        }

        if (string.IsNullOrEmpty(operation.OperationId))
        {
            operation.OperationId = $"{context.MethodInfo.DeclaringType?.Name?.Replace("Controller", "")}_{context.MethodInfo.Name}";
        }
    }

    private bool IsRateLimitedEndpoint(OperationFilterContext context)
    {
        var controllerName = context.MethodInfo.DeclaringType?.Name;
        return controllerName?.Contains("Chat") == true || 
               controllerName?.Contains("Discovery") == true;
    }

    private bool RequiresAuthentication(OperationFilterContext context)
    {
        return context.MethodInfo.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any() ||
               context.MethodInfo.DeclaringType?.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any() == true;
    }

    private bool RequiresAuthorization(OperationFilterContext context)
    {
        var authorizeAttributes = context.MethodInfo.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() ?? Enumerable.Empty<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        
        return authorizeAttributes.Any(attr => !string.IsNullOrEmpty(attr.Roles));
    }

    private string GetControllerName(OperationFilterContext context)
    {
        return context.MethodInfo.DeclaringType?.Name?.Replace("Controller", "") ?? "Unknown";
    }
}