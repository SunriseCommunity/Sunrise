using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Sunrise.Shared.Extensions;

public static class SwaggerAuthExtension
{
    private static readonly string AuthType = "Bearer";

    private static readonly OpenApiSecurityRequirement Requirement = new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = AuthType
                }
            },
            []
        }
    };

    private static readonly OpenApiSecurityScheme Scheme = new()
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    };

    public static void AddJwtAuth(this SwaggerGenOptions option)
    {
        option.AddSecurityDefinition(AuthType, Scheme);
        option.OperationFilter<SecurityRequirementsOperationFilter>();
    }

    private class SecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (
                context.MethodInfo.GetCustomAttributes(true).Any(x => x is AuthorizeAttribute) ||
                (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).Any(x => x is AuthorizeAttribute) ?? false)
            )
            {
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    Requirement
                };
            }
        }
    }
}