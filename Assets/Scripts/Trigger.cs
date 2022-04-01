public class Trigger
{
    public bool IsSet { get; private set; }

    public bool IsFree => !IsSet;

    public bool CheckAndReset()
    {
        var value = IsSet;
        Reset();
        return value;
    }

    public void Set()
    {
        IsSet = true;
    }

    public void Reset()
    {
        IsSet = false;
    }
}
