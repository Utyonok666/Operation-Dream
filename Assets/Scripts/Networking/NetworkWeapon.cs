using Mirror;
using UnityEngine;

public class NetworkWeapon : NetworkBehaviour
{
    [SerializeField] private WeaponController weaponController;

    public override void OnStartAuthority()
    {
        if (weaponController != null)
            weaponController.OnPlayerHit += OnPlayerHit;
    }

    public override void OnStopAuthority()
    {
        if (weaponController != null)
            weaponController.OnPlayerHit -= OnPlayerHit;
    }

    private void OnDestroy()
    {
        if (!isOwned)
            return;

        if (weaponController != null)
            weaponController.OnPlayerHit -= OnPlayerHit;
    }

    private void OnPlayerHit(RaycastHit hit, float damage)
    {
        Debug.Log("OnPlayerHit вызван");


        if (!isOwned)
            return;

        NetworkIdentity identity = hit.collider.GetComponentInParent<NetworkIdentity>();

        if (identity == null)
            return;

        CmdDealDamage(identity.netId, damage);
    }

    [Command]
    private void CmdDealDamage(uint targetNetId, float damage)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity target))
            return;

        Health health = target.GetComponent<Health>();

        if (health == null)
            return;

        if (health.CurrentHealth <= 0)
            return;

        health.TakeDamage(damage);

        if (health.CurrentHealth <= 0)
        {
            PlayerDeath death = target.GetComponent<PlayerDeath>();

            if (death != null)
                death.ServerDie();
        }
    }
}