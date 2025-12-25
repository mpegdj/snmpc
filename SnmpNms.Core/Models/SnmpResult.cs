namespace SnmpNms.Core.Models;

public class SnmpResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public List<SnmpVariable> Variables { get; }
    public long ResponseTime { get; } // ms

    private SnmpResult(bool isSuccess, string? errorMessage, List<SnmpVariable>? variables, long responseTime)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Variables = variables ?? new List<SnmpVariable>();
        ResponseTime = responseTime;
    }

    public static SnmpResult Success(List<SnmpVariable> variables, long responseTime)
    {
        return new SnmpResult(true, null, variables, responseTime);
    }

    public static SnmpResult Fail(string errorMessage)
    {
        return new SnmpResult(false, errorMessage, null, 0);
    }
}

