using Mirror;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private Health health;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private int syncedHealth;

    public override void OnStartServer()
    {
        health.OnHealthChanged += ServerHealthChanged;
        syncedHealth = health.CurrentHealth;
    }

    public override void OnStopServer()
    {
        health.OnHealthChanged -= ServerHealthChanged;
    }

    private void ServerHealthChanged(int newHealth)
    {
        syncedHealth = newHealth;
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (isServer)
            return;

        health.SetHealth(newHealth);
    }
}