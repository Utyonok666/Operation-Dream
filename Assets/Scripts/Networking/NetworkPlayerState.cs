using Mirror;
using UnityEngine;

public class NetworkPlayerState : NetworkBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;

    [SyncVar(hook = nameof(OnCrouchChanged))]
    private bool isCrouching;


    public override void OnStartClient()
    {
        Debug.Log("NetworkPlayerState started. ID: " + netId);
    }


    public void SetCrouch(bool state)
    {
        if (!isLocalPlayer)
            return;

        Debug.Log("Sending crouch: " + state);

        CmdSetCrouch(state);
    }


    [Command]
    private void CmdSetCrouch(bool state)
    {
        Debug.Log("Server received crouch: " + state);

        isCrouching = state;
    }


    private void OnCrouchChanged(bool oldValue, bool newValue)
    {
        Debug.Log("Crouch sync received: " + newValue);

        if (isLocalPlayer)
            return;

        if (playerMovement != null)
            playerMovement.SetNetworkCrouch(newValue);
    }
}