
// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;
//BOM(Byte Order Mark)
public class ScriptEncodingConverter
{
    const string key = "SEC_AutoFix";
    const string menu_to_utf8 = "Assets/Script Encoding Converter/To UTF8";
    const string menu_to_gb2312 = "Assets/Script Encoding Converter/To GB2312";
    const string menu_auto = "Assets/Script Encoding Converter/Auto Fix";
    static bool isConvertManually = false;


    /// <summary> 是否开启Encoding自动修正</summary>
    [MenuItem(menu_auto, priority = 100)]
    static void SwitchAutoFixState()
    {
        var value = !EditorPrefs.GetBool(key, false);
        EditorPrefs.SetBool(key, value);
        Menu.SetChecked(menu_auto, value);
    }
    /// <summary> 初始化 <see cref="menu_auto"/> 菜单的 checked 状态 </summary>
    [MenuItem(menu_auto, validate = true)]
    static bool SwitchAutoFixStateValidate()
    {
        var value = EditorPrefs.GetBool(key, false);
        Menu.SetChecked(menu_auto, value);
        return true;
    }

    /// <summary> 将脚本编码格式转换为 UTF8 </summary>
    [MenuItem(menu_to_utf8)]
    static void Convert2UTF8()
    {
        var settings = new ConvertSettings
        {
            predicate = x => IsNeedConvertToUtf8(x),
            from = Encoding.GetEncoding(936),
            to = new UTF8Encoding(false),
        };
        EncodingConverter(settings);
    }

    /// <summary> 将脚本编码格式转换为 GB2312 (测试用) </summary>
    [MenuItem(menu_to_gb2312)]
    static void Convert2GB2312()
    {
        var settings = new ConvertSettings
        {
            predicate = x => DetectFileEncoding(x, "utf-8"),
            from = Encoding.UTF8,
            to = Encoding.GetEncoding(936),
        };
        EncodingConverter(settings);
    }

    // 因为 DetectFileEncoding 函数判断 gb2312 时，对 utf-8 no bom 返回了true，所以做双重判断
    static bool IsNeedConvertToUtf8(string file) => !DetectFileEncoding(file, "utf-8") && DetectFileEncoding(file, "gb2312");

    public static bool DetectFileEncoding(string file, string name)
    {
        var encodingVerifier = Encoding.GetEncoding(name, new EncoderExceptionFallback(), new DecoderExceptionFallback());
        using (var reader = new StreamReader(file, encodingVerifier, true, 1024))
        {
            try
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                }
                return reader.CurrentEncoding.BodyName == name;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    static void EncodingConverter(ConvertSettings settings)
    {
        MonoScript[] msarr = Selection.GetFiltered<MonoScript>(SelectionMode.DeepAssets);
        if (null != msarr && msarr.Length > 0)
        {
            isConvertManually = true;
            List<string> files = new List<string>();
            foreach (var item in msarr)
            {
                string path = AssetDatabase.GetAssetPath(item);
                if (settings.predicate.Invoke(path))
                {
                    var text = File.ReadAllText(path, settings.from);
                    File.WriteAllText(path, text, settings.to);
                    files.Add(path);
                    AssetDatabase.ImportAsset(path);
                }
            }
            Report("手动处理", files);
            isConvertManually = false;
        }
    }

    class ConvertSettings
    {
        public Func<string, bool> predicate;
        public Encoding from, to;
    }

    class ScriptEncodingAutoFixHandler : AssetPostprocessor
    {
        //所有的资源的导入，删除，移动，都会调用此方法，注意，这个方法是static的
        public static void OnPostprocessAllAssets(string[] importedAsset, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            //如果用户手动处理中，或者没选择自动修正则不处理，不响应此回调
            if (isConvertManually || !EditorPrefs.GetBool(key, false)) return;
            //仅对有修改的脚本进行处理, 内置 Package 包是只读的，避免死循环故而不处理。
            var scripts = importedAsset.Where(v => v.EndsWith(".cs"))
                .Where(v => !Path.GetFullPath(v).Contains("PackageCache"))
                .ToArray();
            List<string> files = new List<string>();
            foreach (var path in scripts)
            {
                //如果是  gb2312 编码就改成 utf-8
                if (IsNeedConvertToUtf8(path))
                {
                    var text = File.ReadAllText(path, Encoding.GetEncoding(936));
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    files.Add(path);
                }
            }
            if (files.Count > 0)
            {
                Report("Auto fix to UTF8", files);
                foreach (var file in files)
                {
                    AssetDatabase.ImportAsset(file);
                }
            }
        }
    }

    #region 为 Log 的文件条目提供超链接，点击可以  Ping/高亮 脚本文件
    [InitializeOnLoadMethod]
    private static void Init()
    {
#if UNITY_2021_1_OR_NEWER
        EditorGUI.hyperLinkClicked += OnLinkClicked;
        static void OnLinkClicked(EditorWindow ew, HyperLinkClickedEventArgs args)
        {
            if (args.hyperLinkData.TryGetValue("sourcefile", out var file))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(file));
            }
        };
     }
#else
        var evt = typeof(EditorGUI).GetEvent("hyperLinkClicked", BindingFlags.Static | BindingFlags.NonPublic);
        if (evt != null)
        {
            var handler = Delegate.CreateDelegate(evt.EventHandlerType, typeof(ScriptEncodingConverter), nameof(OnLinkClicked));
            evt.AddMethod.Invoke(null, new object[] { handler });
        }
    }
    static void OnLinkClicked(object sender, EventArgs args)
    {
        var property = args.GetType().GetProperty("hyperlinkInfos", BindingFlags.Instance | BindingFlags.Public);
        if (property.GetValue(args) is Dictionary<string, string> infos)
        {
            if (infos.TryGetValue("sourcefile", out var file))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(file));
            }
        }
    }
#endif
    static void Report(string title, IEnumerable<string> files)
    {
        // 加入 hyperlink
        files = files.Select(v => $"<a sourcefile=\"{v}\">{v}</a>");
        var info = files.Count() > 0 ? $"处理文件 {files.Count()} 个，更多 ↓ \n{string.Join("\n", files)}" : "没有发现编码问题！";
        Debug.Log($"{title}: {info}");
    }
    #endregion
}
