using UnityEditor.PackageManager.Requests;
public static class UpmRequestExtensions
{
    public static UpmRequestAwaiter GetAwaiter(this Request request)
    {
        return new UpmRequestAwaiter(request);
    }
}
