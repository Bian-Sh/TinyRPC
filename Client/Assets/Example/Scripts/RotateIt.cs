using UnityEngine;

public class RotateIt : MonoBehaviour
{
    public int speed = 5;

    private void Update()
    {
         transform.Rotate(Vector3.up, speed*Time.deltaTime, Space.Self);
    }
}
