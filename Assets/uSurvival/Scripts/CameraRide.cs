using UnityEngine;

public class CameraRide : MonoBehaviour
{
    public float speed = 0.1f;

    void Update()
    {
        // only while not logged in yet
        if (Player.localPlayer) Destroy(this);
        transform.position += Vector3.up * speed * Time.deltaTime;
    }
}
