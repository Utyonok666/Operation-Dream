using Mirror;
using UnityEngine;

public class NetworkLook : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;

    [SyncVar]
    private float lookX;

    public void SetLook(float xRotation)
    {
        if (!isLocalPlayer)
            return;

        CmdSetLook(xRotation);
    }

    [Command]
    private void CmdSetLook(float xRotation)
    {
        lookX = xRotation;
    }

    private void LateUpdate()
    {
        if (isLocalPlayer)
            return;

        Vector3 angles = cameraHolder.localEulerAngles;
        angles.x = lookX;
        cameraHolder.localEulerAngles = angles;
    }
}