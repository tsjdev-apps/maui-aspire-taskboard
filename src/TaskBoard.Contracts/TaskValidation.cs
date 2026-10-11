namespace TaskBoard.Contracts;

/// <summary>Shared input limits for the API and client editor.</summary>
public static class TaskValidation
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;

    public static Dictionary<string, string[]> Validate(string? title, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["A title is required."];
        }
        else if (title.Trim().Length > MaxTitleLength)
        {
            errors["title"] = [$"The title must contain at most {MaxTitleLength} characters."];
        }

        if (description?.Length > MaxDescriptionLength)
        {
            errors["description"] = [$"The description must contain at most {MaxDescriptionLength} characters."];
        }

        return errors;
    }
}
