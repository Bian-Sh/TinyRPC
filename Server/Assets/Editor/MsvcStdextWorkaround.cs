// Adding this script to anywhere within your project.
// FIX ERROR: Building Library\Bee\artifacts\WinPlayerBuildProgram\ei6vj\wncp_y_vm6.lump.obj failed with output: qtky_vm6.lump.cpp
// FROM: https://forum.unity.com/threads/workaround-for-building-with-il2cpp-with-visual-studio-2022-17-4.1355570/
// SEE: https://developercommunity.visualstudio.com/t/stdext::hash_compare-has-been-removed-in/10182319
//      https://issuetracker.unity3d.com/issues/il2cpp-windows-builds-fails-when-using-vs-2022-17-dot-4-0-preview

#if UNITY_EDITOR
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class MsvcStdextWorkaround : IPreprocessBuildWithReport
{
    const string kWorkaroundFlag = "/D_SILENCE_STDEXT_HASH_DEPRECATION_WARNINGS";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var clEnv = Environment.GetEnvironmentVariable("_CL_");

        if (string.IsNullOrEmpty(clEnv))
        {
            Environment.SetEnvironmentVariable("_CL_", kWorkaroundFlag);
        }
        else if (!clEnv.Contains(kWorkaroundFlag))
        {
            clEnv += " " + kWorkaroundFlag;
            Environment.SetEnvironmentVariable("_CL_", clEnv);
        }
    }
}
#endif // UNITY_EDITOR
