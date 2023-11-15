using System;
using System.Collections.Concurrent;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TaskDriver
{
    private readonly static ConcurrentQueue<Action> _actions;
    static TaskDriver()
    {
        _actions = new();
        EditorApplication.update += Update;
    }

    public static void Post(Action action) => _actions.Enqueue(action);

    private static void Update()
    {
        while (_actions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
