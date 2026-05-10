namespace SixToFix.Application.Exceptions;

public sealed class CategoryResultNotFoundException : Exception
{
    public Guid CategoryId { get; }

    public CategoryResultNotFoundException(Guid categoryId)
        : base($"CategoryResult '{categoryId}' was not found.")
    {
        CategoryId = categoryId;
    }
}
