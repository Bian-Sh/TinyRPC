using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
namespace zFramework.TinyRPC
{
    public static class AwaiterEx
    {
        /// <summary>
        /// 切换到主线程中执行
        /// </summary>
        public static SwitchToUnityThreadAwaitable ToMainThread => new();
        /// <summary>
        /// 切换到线程池中执行
        /// </summary>
        public static SwitchToThreadPoolAwaitable ToOtherThread => new();
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == threadid;
        static int threadid;

        static SynchronizationContext context;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            context = SynchronizationContext.Current;
            threadid = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        ///  在主线程中执行
        /// </summary>
        /// <param name="task">要执行的委托</param>
        public static void Post(Action task)
        {
            if (Thread.CurrentThread.ManagedThreadId == 1) //主线程
            {
                task?.Invoke();
            }
            else
            {
                context.Post(_ => task?.Invoke(), null);
            }
        }
        public struct SwitchToUnityThreadAwaitable
        {
            public readonly Awaiter GetAwaiter() => new();
            public struct Awaiter : INotifyCompletion
            {
                public readonly bool IsCompleted => IsMainThread;
                public readonly void GetResult() { }
                public readonly void OnCompleted(Action continuation) => Post(continuation);
            }
        }
        public struct SwitchToThreadPoolAwaitable
        {
            public readonly Awaiter GetAwaiter() => new();
            public readonly struct Awaiter : ICriticalNotifyCompletion
            {
                static readonly WaitCallback switchToCallback = state => ((Action)state).Invoke();
                public bool IsCompleted => false;
                public void GetResult() { }
                public void OnCompleted(Action continuation) => ThreadPool.UnsafeQueueUserWorkItem(switchToCallback, continuation);
                public void UnsafeOnCompleted(Action continuation) => ThreadPool.UnsafeQueueUserWorkItem(switchToCallback, continuation);
            }
        }
    }
}
