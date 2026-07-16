namespace Djehuti.DjeLab.Services;

public class DemoMode
{
    private DemoScript? _currentScript;
    private int _currentStepIndex = -1;
    private CancellationTokenSource? _stepTimer;
    private readonly Dictionary<string, Func<DemoAction, Task>> _actionHandlers = new();

    public event Action? OnStateChanged;
    public event Func<DemoAction, Task>? OnActionExecuting;

    public DemoAnnotationState AnnotationState { get; } = new();

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    public DemoMode()
    {
        RegisterDefaultHandlers();
    }

    public void LoadScript(DemoScript script)
    {
        _currentScript = script;
        _currentStepIndex = -1;
        AnnotationState.TotalSteps = script.Steps.Count;
        NotifyStateChanged();
    }

    public async Task StartDemo()
    {
        if (_currentScript is null || _currentScript.Steps.Count == 0)
            return;

        IsRunning = true;
        IsPaused = false;
        _currentStepIndex = 0;
        await ExecuteCurrentStep();
    }

    public void PauseDemo()
    {
        IsPaused = true;
        _stepTimer?.Cancel();
        NotifyStateChanged();
    }

    public async Task ResumeDemo()
    {
        IsPaused = false;
        await ScheduleNextStep();
    }

    public void StopDemo()
    {
        IsRunning = false;
        IsPaused = false;
        _currentStepIndex = -1;
        _stepTimer?.Cancel();
        AnnotationState.IsVisible = false;
        NotifyStateChanged();
    }

    public async Task AdvanceStep()
    {
        if (_currentScript is null || _currentStepIndex >= _currentScript.Steps.Count - 1)
        {
            StopDemo();
            return;
        }

        _currentStepIndex++;
        await ExecuteCurrentStep();
    }

    private async Task ExecuteCurrentStep()
    {
        if (_currentScript is null || _currentStepIndex < 0 || _currentStepIndex >= _currentScript.Steps.Count)
        {
            StopDemo();
            return;
        }

        var step = _currentScript.Steps[_currentStepIndex];
        AnnotationState.CurrentStepId = step.Id;
        AnnotationState.CurrentStepIndex = _currentStepIndex;
        AnnotationState.Annotation = step.Annotation;
        AnnotationState.PointerTarget = step.PointerTarget;
        AnnotationState.IsVisible = true;

        try
        {
            OnActionExecuting?.Invoke(step.Action);

            if (_actionHandlers.TryGetValue(step.Action.Type, out var handler))
            {
                await handler(step.Action);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Demo action failed: {ex.Message}");
        }

        NotifyStateChanged();
        await ScheduleNextStep();
    }

    private async Task ScheduleNextStep()
    {
        if (!IsRunning || IsPaused || _currentScript is null || _currentStepIndex < 0 || _currentStepIndex >= _currentScript.Steps.Count)
            return;

        var step = _currentScript.Steps[_currentStepIndex];
        _stepTimer = new CancellationTokenSource();

        try
        {
            await Task.Delay(step.DurationMs, _stepTimer.Token);
            if (!_stepTimer.Token.IsCancellationRequested && IsRunning && !IsPaused)
            {
                await AdvanceStep();
            }
        }
        catch (TaskCanceledException)
        {
            // Step was cancelled, that's fine
        }
        finally
        {
            _stepTimer?.Dispose();
            _stepTimer = null;
        }
    }

    private void RegisterDefaultHandlers()
    {
        _actionHandlers["none"] = _ => Task.CompletedTask;
        _actionHandlers["activatePane"] = ExecuteActivatePane;
        _actionHandlers["wait"] = ExecuteWait;
    }

    private Task ExecuteActivatePane(DemoAction action)
    {
        if (action.Params.TryGetValue("paneKind", out var kindObj) && kindObj is string kind)
        {
            var evt = new WorkspaceTourRequested(kind);
            OnActionExecuting?.Invoke(new DemoAction { Type = "activatePane", Params = action.Params });
        }
        return Task.CompletedTask;
    }

    private Task ExecuteWait(DemoAction action)
    {
        if (action.Params.TryGetValue("ms", out var msObj) && msObj is int ms)
        {
            return Task.Delay(ms);
        }
        return Task.CompletedTask;
    }

    public void RegisterActionHandler(string actionType, Func<DemoAction, Task> handler)
    {
        _actionHandlers[actionType] = handler;
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}

// Marker class for workspace tour events
public sealed record WorkspaceTourRequested(string TargetPaneKind);
