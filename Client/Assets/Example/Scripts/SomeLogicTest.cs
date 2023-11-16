#if UNITY_EDITOR
using EasyButtons;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SomeLogicTest : MonoBehaviour
{
    public DefaultAsset proto;

    [Button]
    void TestReadLastWriteTime()
    {
        var path = AssetDatabase.GetAssetPath(proto);
        var time = File.GetLastWriteTime(path);
        var ticks = time.Ticks;
        // log time and ticks
        Debug.Log($"time: {time:F}, ticks: {ticks}");
        var time2 = new DateTime(ticks);
        Debug.Log($"time2: {time2:F}");
    }
}

#endif