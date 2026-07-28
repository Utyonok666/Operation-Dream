using Mirror;
using UnityEngine;

public class PlayerSkin : NetworkBehaviour
{
    public Material[] skins;

    private MeshRenderer meshRenderer;

    [SyncVar(hook = nameof(OnSkinChanged))]
    private int skinIndex;


    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
    }


    public override void OnStartServer()
    {
        skinIndex = Random.Range(0, skins.Length);
    }


    public override void OnStartClient()
    {
        ApplySkin(skinIndex);
    }


    void OnSkinChanged(int oldIndex, int newIndex)
    {
        ApplySkin(newIndex);
    }


    void ApplySkin(int index)
    {
        if (meshRenderer == null) return;
        if (index < 0 || index >= skins.Length) return;

        meshRenderer.material = skins[index];
    }
}