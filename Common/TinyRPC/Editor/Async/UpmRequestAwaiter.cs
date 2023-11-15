using System.Runtime.CompilerServices;
using UnityEditor.PackageManager.Requests;

public readonly struct UpmRequestAwaiter : INotifyCompletion
{
    private readonly Request _request;
    public readonly bool IsCompleted => _request.IsCompleted;
    public UpmRequestAwaiter(Request request) => _request = request;
    public readonly string GetResult() => _request.Error?.message;

    public readonly void OnCompleted(System.Action continuation) => TaskDriver.Post(continuation);
}
