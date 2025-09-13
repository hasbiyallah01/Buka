using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AmalaSpotLocator.Attributes;

public class SafeStringAttribute : ValidationAttribute
{
    private static readonly Regex[] MaliciousPatterns = {
        new(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"javascript:", RegexOptions.IgnoreCase),
        new(@"vbscript:", RegexOptions.IgnoreCase),
        new(@"onload\s*=", RegexOptions.IgnoreCase),
        new(@"onerror\s*=", RegexOptions.IgnoreCase),
        new(@"onclick\s*=", RegexOptions.IgnoreCase),
        new(@"<iframe[^>]*>", RegexOptions.IgnoreCase),
        new(@"<object[^>]*>", RegexOptions.IgnoreCase),
        new(@"<embed[^>]*>", RegexOptions.IgnoreCase)
    };

    public override bool IsValid(object? value)
    {
        if (value == null || value is not string stringValue)
            return true;

        return !MaliciousPatterns.Any(pattern => pattern.IsMatch(stringValue));
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field contains potentially unsafe content.";
    }
}

public class LatitudeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null)
            return true;

        if (value is double doubleValue)
            return doubleValue >= -90 && doubleValue <= 90;

        if (value is decimal decimalValue)
            return decimalValue >= -90 && decimalValue <= 90;

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between -90 and 90 degrees.";
    }
}

public class LongitudeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null)
            return true;

        if (value is double doubleValue)
            return doubleValue >= -180 && doubleValue <= 180;

        if (value is decimal decimalValue)
            return decimalValue >= -180 && decimalValue <= 180;

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between -180 and 180 degrees.";
    }
}

public class NigerianPhoneAttribute : ValidationAttribute
{
    private static readonly Regex PhoneRegex = new(@"^(\+234|0)[789][01]\d{8}$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value == null || value is not string stringValue)
            return true;

        return PhoneRegex.IsMatch(stringValue);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be a valid Nigerian phone number.";
    }
}

public class SafeNameAttribute : ValidationAttribute
{
    private static readonly Regex NameRegex = new(@"^[a-zA-Z0-9\s\-'.,&()]+$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value == null || value is not string stringValue)
            return true;

        return NameRegex.IsMatch(stringValue) && stringValue.Length <= 200;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field contains invalid characters or is too long.";
    }
}

public class RatingAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null)
            return true;

        if (value is int intValue)
            return intValue >= 1 && intValue <= 5;

        if (value is decimal decimalValue)
            return decimalValue >= 1 && decimalValue <= 5;

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between 1 and 5.";
    }
}

public class MaxItemsAttribute : ValidationAttribute
{
    private readonly int _maxItems;

    public MaxItemsAttribute(int maxItems)
    {
        _maxItems = maxItems;
    }

    public override bool IsValid(object? value)
    {
        if (value == null)
            return true;

        if (value is System.Collections.ICollection collection)
            return collection.Count <= _maxItems;

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field cannot have more than {_maxItems} items.";
    }
}