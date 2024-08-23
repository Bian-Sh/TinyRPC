using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
/*
通过 ObjectPool.Allocate 获取的消息实例可以使用 using 语句块可以自动回收
new 出来的消息实例不在对象池管理范围内，不会自动回收
以下为使用示例：
class Foo 
{
    public void Bar()
    {
        using var request = ObjectPool.Allocate<AnyCustomRequest>();
        // do something

        using var response = TinyClient.Call<AnyCustomResponse>(request);
        // do something
    }
}
使用起来非常简单，使用 using 关键字，其他的事情都交给 ObjectPool 来处理
 */

namespace zFramework.TinyRPC
{
    public static class ObjectPool
    {
        public static readonly ConcurrentDictionary<Type, InternalPool> pools = new();
        private static InternalPool GetPool<T>() where T : class, IReusable => GetPool(typeof(T));
        private static InternalPool GetPool(Type type)
        {
            if (!pools.TryGetValue(type, out var pool))
            {
                try
                {
                    pool = new InternalPool(type);
                    pools.TryAdd(type, pool);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(ObjectPool)}: 尝试为类型 {type} 构建对象池失败 ！e = {e}");
                }
            }
            return pool;
        }
        public static T Allocate<T>() where T : class, IReusable => GetPool<T>().Allocate() as T;
        public static void Recycle<T>(T target) where T : class, IReusable
        {
            if (target.RequireRecycle)
            {
                // 避免非池化对象导致构建和占用对象池
                GetPool<T>().Recycle(target);
            }
        }

        public static IReusable Allocate(Type type) => GetPool(type).Allocate();
        public static void Recycle(IReusable target)
        {
            if (target.RequireRecycle)
            {
                GetPool(target.GetType()).Recycle(target);
            }
        }
    }

    public sealed class InternalPool
    {
        internal ConcurrentStack<IReusable> items; //线程安全 栈
        public int Capacity { get; set; } //池子有多大
        public int Counted => counted; //当前池子里有多少
        private int counted;
        private readonly Type type;
        internal InternalPool(Type type, int capacity = 200)
        {
            this.type = type;
            this.Capacity = capacity;
            this.counted = 0;
            items = new ConcurrentStack<IReusable>();
        }
        public IReusable Allocate() //分配
        {
            if (!items.IsEmpty && items.TryPop(out var item))
            {
                Interlocked.Decrement(ref counted);
            }
            else
            {
                item = Activator.CreateInstance(type) as IReusable;
            }
            item.RequireRecycle = true;
            return item;
        }
        public void Recycle(IReusable target) //回收
        {
            if (null != target && target.RequireRecycle)
            {
                target.RequireRecycle = false;
                target.OnRecycle();
                if (counted < Capacity)
                {
                    items.Push(target);
                    Interlocked.Increment(ref counted);
                }
            }
        }
    }
}