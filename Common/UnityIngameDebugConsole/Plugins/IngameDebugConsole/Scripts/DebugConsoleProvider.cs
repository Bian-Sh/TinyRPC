using IngameDebugConsole;
using System.Collections.Generic;
using UnityEngine;

public class DebugConsoleProvider : MonoBehaviour
{
    public DebugLogManager target;
    public float duration = 1f;
    public int numOfCircleToShow = 1;
    public bool showDefault;
    private void Start()
    {
        if (showDefault)
        {
            target.ShowLogWindow();
        }
        else
        {
            target.HideAll();
        }
    }
    private void Update()
    {
        if (isGestureDone())
        {
            target.SwitchPopupState();
        }
    }

    //写一个方法，实现在屏幕上画一个圈切换 taget 显示隐藏的状态
    List<Vector2> gestureDetector = new List<Vector2>();
    Vector2 gestureSum = Vector2.zero;
    float gestureLength = 0;
    int gestureCount = 0;
    float cachedtime = 0;
    float dragduration = 0;
    bool isGestureDone()
    {

        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            if (Input.touches.Length != 1) return false;

            var touch = Input.touches[0];
            if (touch.phase == TouchPhase.Began)
            {
                gestureCount = 0;
                dragduration = 0;
                cachedtime = Time.realtimeSinceStartup;
            }
            else if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
            {
                gestureDetector.Clear();
            }
            else if (touch.phase == TouchPhase.Moved && dragduration < duration)
            {
                Vector2 p = touch.position;
                if (gestureDetector.Count == 0 || (p - gestureDetector[gestureDetector.Count - 1]).magnitude > 10)
                    gestureDetector.Add(p);
            }
            dragduration = Time.realtimeSinceStartup - cachedtime;
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                cachedtime = Time.realtimeSinceStartup;
                dragduration = 0;
            }
            if (Input.GetMouseButtonUp(0))
            {
                gestureDetector.Clear();
                gestureCount = 0;
            }
            else
            {
                if (Input.GetMouseButton(0) && dragduration < duration)
                {
                    Vector2 p = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                    if (gestureDetector.Count == 0 || (p - gestureDetector[gestureDetector.Count - 1]).magnitude > 10)
                        gestureDetector.Add(p);
                }
                dragduration = Time.realtimeSinceStartup - cachedtime;
            }
        }
        if (dragduration > duration) return false;
        if (gestureDetector.Count < 10) return false;
        gestureSum = Vector2.zero;
        gestureLength = 0;
        Vector2 prevDelta = Vector2.zero;
        for (int i = 0; i < gestureDetector.Count - 2; i++)
        {

            Vector2 delta = gestureDetector[i + 1] - gestureDetector[i];
            float deltaLength = delta.magnitude;
            gestureSum += delta;
            gestureLength += deltaLength;

            float dot = Vector2.Dot(delta, prevDelta);
            if (dot < 0f)
            {
                gestureDetector.Clear();
                gestureCount = 0;
                return false;
            }

            prevDelta = delta;
        }

        int gestureBase = (Screen.width + Screen.height) / 4;

        if (gestureLength > gestureBase && gestureSum.magnitude < gestureBase / 2)
        {
            gestureDetector.Clear();
            gestureCount++;
            if (gestureCount >= numOfCircleToShow)
                return true;
        }

        return false;
    }
}
