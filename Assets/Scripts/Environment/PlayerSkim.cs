using Mirror;
using UnityEngine;

// ============================================================
// PlayerSkin
// Randomly assigns a skin material on spawn, synced to all clients.
// Случайно назначает материал скина при спавне, синхронизирует всем клиентам.
// ============================================================
public class PlayerSkin : NetworkBehaviour
{
    public Material[] skins;

    private MeshRenderer meshRenderer;

    // SyncVar: server sets it, Mirror auto-syncs the value to all clients.
    // hook = calls OnSkinChanged automatically whenever the value changes on a client.
    // SyncVar: сервер выставляет, Mirror сам синхронизирует значение всем клиентам.
    // hook = автоматически вызывает OnSkinChanged при изменении значения на клиенте.
    [SyncVar(hook = nameof(OnSkinChanged))]
    private int skinIndex;


    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
    }


    // Runs only on the server: pick the random skin once, authoritatively
    // Выполняется только на сервере: выбираем случайный скин один раз, авторитетно
    public override void OnStartServer()
    {
        skinIndex = Random.Range(0, skins.Length);
    }


    // Runs on every client (including host): apply whatever skinIndex already arrived
    // Выполняется на каждом клиенте (включая хост): применяем уже пришедший skinIndex
    public override void OnStartClient()
    {
        ApplySkin(skinIndex);
    }


    // SyncVar hook signature must be (oldValue, newValue) // Сигнатура хука SyncVar обязательно (oldValue, newValue)
    void OnSkinChanged(int oldIndex, int newIndex)
    {
        ApplySkin(newIndex);
    }


    void ApplySkin(int index)
    {
        if (meshRenderer == null) return;
        if (index < 0 || index >= skins.Length) return; // Guard against bad/empty array // Защита от плохого/пустого массива

        meshRenderer.material = skins[index];
    }
}