using EasyButtons;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using zFramework.TinyRPC;

public class SomeLogicTest : MonoBehaviour
{

    [Button("注册 消息和消息处理器")]
    void TestJsonUtility()
    {
        MessageManager.Awake();
    }


}
