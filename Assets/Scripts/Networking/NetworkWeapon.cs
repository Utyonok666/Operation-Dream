using Mirror;
using UnityEngine;

public class NetworkWeapon : NetworkBehaviour
{
    [SerializeField] private WeaponController weaponController;

    public override void OnStartAuthority()
    {
        weaponController.OnPlayerHit += OnPlayerHit;
    }

    public override void OnStopAuthority()
    {
        weaponController.OnPlayerHit -= OnPlayerHit;
    }

    private void OnDestroy()
    {
        if (!isOwned)
            return;

        weaponController.OnPlayerHit -= OnPlayerHit;
    }

    private void OnPlayerHit(RaycastHit hit, float damage)
    {
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

        health.TakeDamage(Mathf.RoundToInt(damage));
    }
}