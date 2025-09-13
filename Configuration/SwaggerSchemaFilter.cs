using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Configuration;

public class SwaggerSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {

        if (context.Type == typeof(Guid) || context.Type == typeof(Guid?))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString("123e4567-e89b-12d3-a456-426614174000");
        }
        else if (context.Type == typeof(DateTime) || context.Type == typeof(DateTime?))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString("2024-01-15T10:30:00Z");
        }
        else if (context.Type == typeof(TimeSpan) || context.Type == typeof(TimeSpan?))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiString("09:00:00");
        }

        var properties = context.Type.GetProperties();
        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property.Name);
            if (schema.Properties?.ContainsKey(propertyName) == true)
            {
                var propertySchema = schema.Properties[propertyName];

                var requiredAttribute = property.GetCustomAttributes(typeof(RequiredAttribute), false).FirstOrDefault() as RequiredAttribute;
                if (requiredAttribute != null)
                {
                    if (schema.Required == null)
                        schema.Required = new HashSet<string>();
                    schema.Required.Add(propertyName);
                }

                var rangeAttribute = property.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;
                if (rangeAttribute != null)
                {
                    if (rangeAttribute.Minimum is double min)
                        propertySchema.Minimum = (decimal)min;
                    if (rangeAttribute.Maximum is double max)
                        propertySchema.Maximum = (decimal)max;
                }

                var stringLengthAttribute = property.GetCustomAttributes(typeof(StringLengthAttribute), false).FirstOrDefault() as StringLengthAttribute;
                if (stringLengthAttribute != null)
                {
                    propertySchema.MaxLength = stringLengthAttribute.MaximumLength;
                    if (stringLengthAttribute.MinimumLength > 0)
                        propertySchema.MinLength = stringLengthAttribute.MinimumLength;
                }

                var minLengthAttribute = property.GetCustomAttributes(typeof(MinLengthAttribute), false).FirstOrDefault() as MinLengthAttribute;
                if (minLengthAttribute != null)
                {
                    propertySchema.MinLength = minLengthAttribute.Length;
                }

                var maxLengthAttribute = property.GetCustomAttributes(typeof(MaxLengthAttribute), false).FirstOrDefault() as MaxLengthAttribute;
                if (maxLengthAttribute != null)
                {
                    propertySchema.MaxLength = maxLengthAttribute.Length;
                }
            }
        }

        AddTypeSpecificExamples(schema, context.Type);
    }

    private void AddTypeSpecificExamples(OpenApiSchema schema, Type type)
    {
        if (type.Name.Contains("Location"))
        {
            if (schema.Properties?.ContainsKey("latitude") == true)
                schema.Properties["latitude"].Example = new Microsoft.OpenApi.Any.OpenApiDouble(6.5244);
            if (schema.Properties?.ContainsKey("longitude") == true)
                schema.Properties["longitude"].Example = new Microsoft.OpenApi.Any.OpenApiDouble(3.3792);
        }
        else if (type.Name.Contains("Chat"))
        {
            if (schema.Properties?.ContainsKey("message") == true)
                schema.Properties["message"].Example = new Microsoft.OpenApi.Any.OpenApiString("Where can I find good amala near Ikeja?");
            if (schema.Properties?.ContainsKey("sessionId") == true)
                schema.Properties["sessionId"].Example = new Microsoft.OpenApi.Any.OpenApiString("chat_123e4567-e89b-12d3-a456-426614174000");
        }
        else if (type.Name.Contains("Spot"))
        {
            if (schema.Properties?.ContainsKey("name") == true)
                schema.Properties["name"].Example = new Microsoft.OpenApi.Any.OpenApiString("Mama Cass Amala Joint");
            if (schema.Properties?.ContainsKey("address") == true)
                schema.Properties["address"].Example = new Microsoft.OpenApi.Any.OpenApiString("123 Allen Avenue, Ikeja, Lagos");
            if (schema.Properties?.ContainsKey("phoneNumber") == true)
                schema.Properties["phoneNumber"].Example = new Microsoft.OpenApi.Any.OpenApiString("+234 801 234 5678");
        }
        else if (type.Name.Contains("Review"))
        {
            if (schema.Properties?.ContainsKey("rating") == true)
                schema.Properties["rating"].Example = new Microsoft.OpenApi.Any.OpenApiInteger(4);
            if (schema.Properties?.ContainsKey("comment") == true)
                schema.Properties["comment"].Example = new Microsoft.OpenApi.Any.OpenApiString("Great amala and ewedu! The service was excellent.");
        }
    }

    private string GetPropertyName(string propertyName)
    {

        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
            return propertyName;
        
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}