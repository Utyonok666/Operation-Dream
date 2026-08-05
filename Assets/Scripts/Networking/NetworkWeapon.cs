using Mirror;
using UnityEngine;

// ============================================================
// NetworkWeapon
// Connects weapon hits on local clients with server-authoritative health damage.
// Связывает попадания оружия на клиенте с авторитетным нанесением урона на сервере.
// Listens to local raycast hits and sends network Commands to apply damage safely.
// Перехватывает локальные рэйкаст-попадания и отправляет сетевые команды для урона.
// ============================================================
public class NetworkWeapon : NetworkBehaviour
{
    [SerializeField] private WeaponController weaponController;

    // Called on the owning client when network authority over this object starts
    // Вызывается на клиенте-владельце при получении авторитета (прав владения) над объектом
    public override void OnStartAuthority()
    {
        if (weaponController != null)
            weaponController.OnPlayerHit += OnPlayerHit;
    }

    // Called when authority is revoked or removed on this client
    // Вызывается при отзыве или снятии прав владения на этом клиенте
    public override void OnStopAuthority()
    {
        if (weaponController != null)
            weaponController.OnPlayerHit -= OnPlayerHit;
    }

    private void OnDestroy()
    {
        // Safety cleanup to avoid dangling event references on local owner destruction
        // Защитная отписка от событий при уничтожении локального владельца
        if (!isOwned)
            return;

        if (weaponController != null)
            weaponController.OnPlayerHit -= OnPlayerHit;
    }

    // Local hit callback from WeaponController raycasts
    // Локальный коллбэк попадания из рэйкаста WeaponController
    private void OnPlayerHit(RaycastHit hit, float damage)
    {
        Debug.Log("OnPlayerHit вызван");

        if (!isOwned)
            return;

        // Find NetworkIdentity on hit target to get its netId for RPC transmission
        // Ищем NetworkIdentity у подбитой цели, чтобы получить её netId для передачи по сети
        NetworkIdentity identity = hit.collider.GetComponentInParent<NetworkIdentity>();

        if (identity == null)
            return;

        // Send hit target netId and damage value to server
        // Отправляем netId цели и значение урона на сервер
        CmdDealDamage(identity.netId, damage);
    }

    // Command sent from local client owner to apply authoritative damage on server
    // Команда от локального владельца для авторитетного нанесения урона на сервере
    [Command]
    private void CmdDealDamage(uint targetNetId, float damage)
    {
        // Resolve target object reference on server using active netId registry
        // Находим объект цели на сервере через реестр активных сетевых ID
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity target))
            return;

        Health health = target.GetComponent<Health>();

        if (health == null)
            return;

        if (health.CurrentHealth <= 0)
            return;

        // Apply damage on server; Health handles internal state and NetworkHealth syncs it
        // Наносим урон на сервере; Health обновит состояние, а NetworkHealth синхронизирует его
        health.TakeDamage(damage);

        // Trigger server-side death logic if damage resulted in kill
        // Запускаем серверную логику смерти, если урон привела к гибели
        if (health.CurrentHealth <= 0)
        {
            PlayerDeath death = target.GetComponent<PlayerDeath>();

            if (death != null)
                death.ServerDie();
        }
    }
}