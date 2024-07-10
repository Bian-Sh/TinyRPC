using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Messages;

[Serializable]
    public class PoolInfo
    {
        public string Type;
        public int Capacity;
        public int Counted;
    }
// 读取 Object Pool 内数据并展示在 Inspector 窗口
public class ObjectPoolVisualization : MonoBehaviour
{
    public List<PoolInfo> poolInfos_warp = new List<PoolInfo>();
    void Update()
    {
        poolInfos_warp.Clear();
        foreach (var pool in ObjectPool.pools)
        {
            var poolInfo = new PoolInfo();
            poolInfo.Type = pool.Key.ToString();
            poolInfo.Capacity = pool.Value.Capacity;
            poolInfo.Counted = pool.Value.Counted;
            poolInfos_warp.Add(poolInfo);
        }
    }


}
