using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace zFramework.TinyRPC
{
    public class ObjectPool
    {
        readonly ConcurrentDictionary<Type, InternalPool> pools = new();
        private InternalPool GetPool<T>() where T : class, IReusable
        {
            if (!pools.TryGetValue(typeof(T), out var pool))
            {
                try
                {
                    var type = typeof(T);
                    pool = new InternalPool(type);
                    pools.TryAdd(type, pool);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(ObjectPool)}: 尝试为类型 {typeof(T)} 构建对象池失败 ！e = {e}");
                }
            }
            return pool;
        }

        private InternalPool GetPool(Type type)
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

        public T Allocate<T>() where T : class, IReusable => GetPool<T>().Allocate() as T;
        public void Recycle<T>(T target) where T : class, IReusable => GetPool<T>().Recycle(target);
        public IReusable Allocate(Type type) => GetPool(type).Allocate();
        public void Recycle(IReusable target) => GetPool(target.GetType()).Recycle(target);
    }

    public sealed class InternalPool
    {
        internal ConcurrentStack<object> items; //线程安全 栈

        public int Capacity { get; set; } //池子有多大
        private int counted;
        private Type type;
        internal InternalPool(Type type, int capacity = 60)
        {
            this.type = type;
            this.Capacity = capacity;
            this.counted = 0;
            items = new ConcurrentStack<object>();
        }
        public IReusable Allocate() //分配
        {
            if (!items.IsEmpty && items.TryPop(out var item))
            {
                var reusable = item as IReusable;
                reusable.IsRecycled = false;
                Interlocked.Decrement(ref counted);
                return reusable;
            }
            item = Activator.CreateInstance(type);
            return item as IReusable;
        }
        public void Recycle(IReusable target) //回收
        {
            if (null != target && !target.IsRecycled)
            {
                target.OnRecycle();
                Interlocked.Increment(ref counted);
                Interlocked.CompareExchange(ref counted, Capacity, Capacity);
                if (counted != Capacity)
                {
                    items.Push(target);
                }
            }
        }
    }
}