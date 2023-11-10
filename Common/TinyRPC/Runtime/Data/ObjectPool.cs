using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace zFramework.TinyRPC
{
    public static class ObjectPool
    {
        static readonly Dictionary<Type, object> pools = new();
        static ObjectPool<T> GetPool<T>() where T : new()
        {
            if (!pools.TryGetValue(typeof(T), out object pool))
            {
                pool = new ObjectPool<T>();
                pools.Add(typeof(T), pool);
            }
            return (ObjectPool<T>)pool;
        }
        public static void Recycle<T>(T target) where T : new() => GetPool<T>().Recycle(target);
        public static T Allocate<T>() where T : new() => GetPool<T>().Allocate();
    }

    public sealed class ObjectPool<T> where T : new()
    {
        internal ConcurrentStack<T> items; //线程安全 栈
        public int Capacity { get; set; } //池子有多大
        internal ObjectPool(int capacity = 600)
        {
            this.Capacity = capacity;
            items = new ConcurrentStack<T>();
        }
        public T Allocate() //分配
        {
            if (items.IsEmpty || !items.TryPop(out T item))
            {
                item = new T();
            }
            return item;
        }
        public void Recycle(T target) //回收
        {
            if (items.Count < Capacity)
            {
                items.Push(target);
            }
        }
    }
}