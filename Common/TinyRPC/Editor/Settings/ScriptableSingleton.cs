using System;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace zFramework.TinyRPC.Editors
{
    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T s_Instance;
        public static T Instance
        {
            get
            {
                TryLoadOrCreate();
                return s_Instance;
            }
        }

        /// <summary>
        ///  尝试加载或创建单例
        /// </summary>
        /// <param name="forceReload"> 是否强制加载，当且仅当编辑器重新获得焦点时，用于同步外部对配置文件的修改 </param>
        /// <returns></returns>
        public static T TryLoadOrCreate(bool forceReload = false)
        {
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // 为避免尚未落盘数据丢失，当且仅当实例丢失时才加载
                if (forceReload || !s_Instance)
                {
                    var arr = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
                    s_Instance = arr.Length > 0 ? arr[0] as T : s_Instance ?? CreateInstance<T>();
                }
            }
            else
            {
                Debug.LogError($"{nameof(ScriptableSingleton<T>)}: 请指定单例存档路径！ ");
            }
            return s_Instance;
        }

        public static void Save(bool saveAsText = true)
        {
            if (!s_Instance)
            {
                Debug.LogError("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                UnityEngine.Object[] obj = new T[1] { s_Instance };
                InternalEditorUtility.SaveToSerializedFileAndForget(obj, filePath, saveAsText);
            }
        }
        protected static string GetFilePath()
        {
            return typeof(T).GetCustomAttributes(inherit: true)
                  .Cast<FilePathAttribute>()
                  .FirstOrDefault(v => v != null)
                  ?.filepath;
        }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class FilePathAttribute : Attribute
    {
        internal string filepath;
        /// <summary>
        /// 单例存放路径
        /// </summary>
        /// <param name="path">相对 Project 路径</param>
        public FilePathAttribute(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Invalid relative path (it is empty)");
            }
            if (path[0] == '/')
            {
                path = path.Substring(1);
            }
            filepath = path;
        }
    }
}