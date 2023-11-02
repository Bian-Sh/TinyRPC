using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace zFramework.TinyRPC.Editor
{
    [FilePath("ProjectSettings/TinyRpcEditorSettings.asset")]
    public class TinyRpcEditorSettings : ScriptableSingleton<TinyRpcEditorSettings>
    {
        public List<DefaultAsset> protos;
        [SerializeField]
        private List<ProtoModifiedInfo> protoModifiedInfos;
    }

    // 存储 proto 文件的最后修改时间
    // 用于判断是否需要为 proto 文件重新生成 cs 文件
    [Serializable]
    class ProtoModifiedInfo
    {
        public string name;
        // 记录最后修改时间的 ticks，
        // 当用户点击 生成 按钮时，如果获取这些文件的最后修改时间与此处记录的不一致则生成，反之不生成
        // 此值仅在用户点击 生成 按钮时新增/更新，不会因为 protos 列表变化而变化
        public long lastWriteTime; 
    }
}