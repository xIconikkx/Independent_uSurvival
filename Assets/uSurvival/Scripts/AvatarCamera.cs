using UnityEngine;
using Mirror;

public class AvatarCamera : MonoBehaviour
{
    public NetworkIdentity owner;
    public Camera avatarCamera;

    void Start()
    {
        avatarCamera.enabled = owner.isLocalPlayer;
    }
}
