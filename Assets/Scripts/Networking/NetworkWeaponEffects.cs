using Mirror;
using UnityEngine;

public class NetworkWeaponEffects : NetworkBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    public override void OnStartAuthority()
    {
        if (weaponSwitcher == null) return;

        // Находим ВСЕ WeaponController на игроке (даже неактивные, параметр true)
        WeaponController[] allWeapons = weaponSwitcher.GetComponentsInChildren<WeaponController>(true);
        
        foreach (var weapon in allWeapons)
        {
            // Подписываемся на выстрел каждой пушки
            weapon.OnWeaponFired += LocalWeaponFired;
        }
    }

    public override void OnStopAuthority()
    {
        if (weaponSwitcher == null) return;
        
        WeaponController[] allWeapons = weaponSwitcher.GetComponentsInChildren<WeaponController>(true);
        
        foreach (var weapon in allWeapons)
        {
            // Отписываемся при уничтожении объекта/отключении
            weapon.OnWeaponFired -= LocalWeaponFired;
        }
    }

    private void LocalWeaponFired()
    {
        // Вызывается локально, когда ЛЮБАЯ наша пушка стреляет
        CmdPlayShootEffect();
    }

    [Command]
    private void CmdPlayShootEffect()
    {
        // Сервер говорит всем клиентам проиграть эффект
        RpcPlayShootEffect();
    }

    [ClientRpc]
    private void RpcPlayShootEffect()
    {
        if (isLocalPlayer)
            return;

        // Вызываем ПОЛНЫЙ визуал (вместе с фейковыми лучами для трейсеров)
        if (weaponSwitcher != null && weaponSwitcher.ActiveWeapon != null)
        {
            weaponSwitcher.ActiveWeapon.PlayRemoteVisuals();
        }
    }
}