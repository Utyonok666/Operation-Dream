using Mirror;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private PlayerHUD playerHUD;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    public override void OnStartAuthority()
    {
        EnableLocalPlayer(true);
    }

    public override void OnStartClient()
    {
        if (!isOwned)
            EnableLocalPlayer(false);
    }

    private void EnableLocalPlayer(bool value)
    {
        playerMovement.enabled = value;
        mouseLook.enabled = value;

        if (playerHUD != null)
            playerHUD.gameObject.SetActive(value);

        if (playerCamera != null)
            playerCamera.enabled = value;

        if (audioListener != null)
            audioListener.enabled = value;
    }
}