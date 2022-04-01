public class TimedTrigger
{
    private float _setTimeRemained;
    private float _resetTimeRemained;

    private readonly Trigger _trigger = new Trigger();

    public bool IsSet => _trigger.IsSet;
    public bool IsFree => _trigger.IsFree;

    public void Set()
    {
        _trigger.Set();
        _setTimeRemained = 0;
    }

    public void SetIn(float time)
    {
        _setTimeRemained = time;
    }

    public void SetFor(float time)
    {
        Set();
        ResetIn(time);
    }

    public void Reset()
    {
        _trigger.Reset();
        _resetTimeRemained = 0;
    }

    public void ResetIn(float time)
    {
        _resetTimeRemained = time;
    }

    public bool CheckAndReset()
    {
        if (IsSet)
        {
            _resetTimeRemained = 0;
        }
        return _trigger.CheckAndReset();
    }

    public void Step(float timeStep)
    {
        StepReset(timeStep);
        StepSet(timeStep);
    }

    private void StepSet(float timeStep)
    {
        if (_setTimeRemained <= 0 || IsSet)
        {
            return;
        }

        _setTimeRemained -= timeStep;
        if (_setTimeRemained > 0)
        {
            return;
        }

        _setTimeRemained = 0;
        Set();
    }

    private void StepReset(float timeStep)
    {
        if (_resetTimeRemained <= 0 || IsFree)
        {
            return;
        }

        _resetTimeRemained -= timeStep;
        if (_resetTimeRemained > 0)
        {
            return;
        }

        _resetTimeRemained = 0;
        Reset();
    }
}
