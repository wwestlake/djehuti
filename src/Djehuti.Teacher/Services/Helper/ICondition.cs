namespace Djehuti.Teacher.Services.Helper;

public interface ICondition
{
    string Name { get; }
    Task<bool> EvaluateAsync();
}
