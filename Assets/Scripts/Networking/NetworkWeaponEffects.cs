using Mirror;
using UnityEngine;

// ============================================================
// NetworkWeaponEffects
// Synchronizes audio and visual weapon effects (shots, bullet tracers, reloads) across clients.
// Синхронизирует аудио и визуальные эффекты оружия (выстрелы, трейсеры, перезарядку) между клиентами.
// Listens to local weapon events and broadcasts effect triggers to remote client instances.
// Подписывается на локальные события оружия и рассылает триггеры эффектов другим игрокам.
// ============================================================
public class NetworkWeaponEffects : NetworkBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    // Called on local client when authority over this player/weapon set is granted
    // Вызывается на локальном клиенте при получении авторитета (прав владения)
    public override void OnStartAuthority()
    {
        if (weaponSwitcher == null) return;

        // Find ALL WeaponController instances on the player (including inactive ones)
        // Находим ВСЕ WeaponController на игроке (включая неактивное оружие через true)
        WeaponController[] allWeapons = weaponSwitcher.GetComponentsInChildren<WeaponController>(true);
        
        foreach (var weapon in allWeapons)
        {
            // Subscribe to local firing and reloading events for each weapon
            // Подписываемся на события выстрела и начала перезарядки каждого оружия
            weapon.OnWeaponFired += LocalWeaponFired;
            weapon.OnWeaponReloadStarted += LocalWeaponReloaded;
        }
    }

    // Called when authority is revoked or object is disabled/destroyed
    // Вызывается при снятии прав владения или отключении/уничтожении объекта
    public override void OnStopAuthority()
    {
        if (weaponSwitcher == null) return;
        
        WeaponController[] allWeapons = weaponSwitcher.GetComponentsInChildren<WeaponController>(true);
        
        foreach (var weapon in allWeapons)
        {
            // Clean up subscriptions to avoid memory leaks or dangling event triggers
            // Отписываемся от событий для предотвращения утечек памяти и висячих ссылок
            weapon.OnWeaponFired -= LocalWeaponFired;
            weapon.OnWeaponReloadStarted -= LocalWeaponReloaded;
        }
    }

    // Triggered locally whenever any owned weapon fires
    // Вызывается локально, когда любое подконтрольное оружие производит выстрел
    private void LocalWeaponFired()
    {
        CmdPlayShootEffect();
    }

    // Command sent from shooting client to server to trigger RPC
    // Команда отправляется со стреляющего клиента на сервер для вызова RPC
    [Command]
    private void CmdPlayShootEffect()
    {
        RpcPlayShootEffect();
    }

    // Broadcasts shoot visuals/sfx to all clients except the local shooter
    // Рассылает визуал/звук выстрела всем клиентам, кроме самого стрелка
    [ClientRpc]
    private void RpcPlayShootEffect()
    {
        // Shooter already played local audio/visuals instantly with zero latency
        // Стрелок уже проиграл эффекты локально без сетевой задержки, пропускаем
        if (isLocalPlayer)
            return;

        // Play full remote visuals (muzzle flash, tracer rays, 3D audio) on active weapon
        // Проигрываем полный визуал (вспышку, трейсеры, 3D-звук) на активном пушке у сторонних игроков
        if (weaponSwitcher != null && weaponSwitcher.ActiveWeapon != null)
        {
            weaponSwitcher.ActiveWeapon.PlayRemoteVisuals();
        }
    }

    // Triggered locally when active weapon reload starts
    // Вызывается локально в момент старта перезарядки активного оружия
    private void LocalWeaponReloaded()
    {
        CmdPlayReloadEffect();
    }

    // Command sent to server to replicate reload sound to other clients
    // Команда отправляется на сервер для трансляции звука перезарядки другим игрокам
    [Command]
    private void CmdPlayReloadEffect()
    {
        RpcPlayReloadEffect();
    }

    // Broadcasts reload audio to remote clients
    // Рассылает звук перезарядки сторонним клиентам
    [ClientRpc]
    private void RpcPlayReloadEffect()
    {
        // Local player already hears their own reload audio
        // Локальный игрок уже слышит свой звук перезарядки, пропускаем
        if (isLocalPlayer)
            return;

        if (weaponSwitcher != null && weaponSwitcher.ActiveWeapon != null)
        {
            weaponSwitcher.ActiveWeapon.PlayReloadAudio();
        }
    }
}