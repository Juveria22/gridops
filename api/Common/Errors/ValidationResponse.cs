using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GridOps.Api.Common.Errors;

// builds the automatic 400 from [ApiController]
// - camelCase keys for query + body (pageSize, borough)
// - JSON parse errors: no .NET type names, no extra "request is required"
public static class ValidationResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var parameterNames = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet();
        var hasJsonErrors = context.ModelState.Keys.Any(k => k.StartsWith('$'));

        var cleaned = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0) continue;
            // body didn't parse -> "request is required" is just noise
            if (hasJsonErrors && parameterNames.Contains(key)) continue;

            var isJsonError = key.StartsWith('$');
            var field = key.TrimStart('$', '.');
            if (field.Length == 0) field = "body"; // whole body unreadable
            foreach (var error in entry.Errors)
                cleaned.AddModelError(ToCamelCase(field), isJsonError ? "Invalid value." : error.ErrorMessage);
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, cleaned);

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string ToCamelCase(string key) =>
        string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
