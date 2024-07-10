using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace zFramework.TinyRPC
{
    public static class ObjectPool
    {
        static readonly ConcurrentDictionary<Type, InternalPool> pools = new();
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
        public static void Recycle<T>(T target) where T : class, IReusable => GetPool<T>().Recycle(target);
        public static IReusable Allocate(Type type) => GetPool(type).Allocate();
        public static void Recycle(IReusable target) => GetPool(target.GetType()).Recycle(target);
    }

    public sealed class InternalPool
    {
        internal ConcurrentStack<IReusable> items; //线程安全 栈
        public int Capacity { get; set; } //池子有多大
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
            //只要是通过 Allocate API获取的实例都会被标记为需要回收
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
