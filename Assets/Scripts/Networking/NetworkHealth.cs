using Mirror;
using UnityEngine;

// ============================================================
// NetworkHealth
// Syncs server-authoritative Health component state across the network using Mirror.
// Синхронизирует серверное состояние Health по сети через Mirror.
// Connects local Health events to SyncVar hooks to keep client components up to date.
// Связывает локальные события Health с SyncVar-хуками для поддержания актуальности данных на клиентах.
// ============================================================
public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private Health health;

    // Network-synced health value, triggers hook on clients when modified on server
    // Синхронизируемое значение здоровья; вызывает хук на клиентах при изменении на сервере
    [SyncVar(hook = nameof(OnHealthChanged))]
    private int syncedHealth;

    public override void OnStartServer()
    {
        if (health == null)
            health = GetComponent<Health>();

        // Subscribe to server-side health changes to propagate them to clients
        // Подписываемся на изменения здоровья на сервере для последующей рассылки клиентам
        health.OnHealthChanged += ServerHealthChanged;

        syncedHealth = health.CurrentHealth;
    }

    public override void OnStopServer()
    {
        // Clean up subscription to avoid dangling references on server shutdown
        // Отписываемся при остановке сервера во избежание висячих ссылок
        if (health != null)
            health.OnHealthChanged -= ServerHealthChanged;
    }

    private void ServerHealthChanged(int newHealth)
    {
        // Updating SyncVar automatically queues network synchronization to clients
        // Изменение SyncVar автоматически отправляет данные клиентам по сети
        syncedHealth = newHealth;
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        // Server already holds authoritative state; skip redundant local updates
        // Сервер уже имеет авторитетное значение, пропускаем лишнее обновление
        if (isServer)
            return;

        // Apply updated health value to local Health component on remote clients
        // Применяем обновленное значение в локальный компонент Health на клиентах
        health.SetHealth(newHealth);
    }
}