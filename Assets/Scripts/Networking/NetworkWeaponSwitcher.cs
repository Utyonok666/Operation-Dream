using Mirror;
using UnityEngine;

public class NetworkWeaponSwitcher : NetworkBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [SyncVar(hook = nameof(OnWeaponChanged))]
    private int currentWeaponIndex;

    public override void OnStartAuthority()
    {
        weaponSwitcher.OnWeaponChanged += LocalWeaponChanged;
    }

    public override void OnStopAuthority()
    {
        weaponSwitcher.OnWeaponChanged -= LocalWeaponChanged;
    }

    private void LocalWeaponChanged(int index)
    {
        CmdChangeWeapon(index);
    }

    [Command]
    private void CmdChangeWeapon(int index)
    {
        currentWeaponIndex = index;
    }

    private void OnWeaponChanged(int oldIndex, int newIndex)
    {
        weaponSwitcher.SetWeapon(newIndex, false);
    }
}