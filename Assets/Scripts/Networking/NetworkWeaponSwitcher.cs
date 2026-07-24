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
 
   public override void OnStartClient()
    {
        weaponSwitcher.SetWeapon(currentWeaponIndex, false);
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
        // Владелец уже сам переключил оружие.
        if (isOwned)
            return;

        weaponSwitcher.SetWeapon(newIndex, false);
    }
}