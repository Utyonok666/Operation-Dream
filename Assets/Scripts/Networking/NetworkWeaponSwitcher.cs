using Mirror;
using UnityEngine;

// ============================================================
// NetworkWeaponSwitcher
// Synchronizes active weapon switching across the network using Mirror.
// Синхронизирует смену активного оружия по сети через Mirror.
// Connects WeaponSwitcher events to SyncVar hooks to update remote player models.
// Связывает события WeaponSwitcher с SyncVar-хуками для смены оружия у сторонних игроков.
// ============================================================
public class NetworkWeaponSwitcher : NetworkBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    // Network-synced index of currently active weapon slot
    // Синхронизируемый по сети индекс активного слота оружия
    [SyncVar(hook = nameof(OnWeaponChanged))]
    private int currentWeaponIndex;

    // Subscribe to local weapon switch events when player authority starts
    // Подписываемся на локальное событие смены оружия при получении прав владения
    public override void OnStartAuthority()
    {
        weaponSwitcher.OnWeaponChanged += LocalWeaponChanged;
    }

    // Initialize current weapon index on late-joining clients
    // Инициализируем текущее оружие для подключившихся клиентов
    public override void OnStartClient()
    {
        weaponSwitcher.SetWeapon(currentWeaponIndex, false);
    }

    // Unsubscribe when authority is revoked to avoid dangling event references
    // Отписываемся при отзыве прав владения во избежание висячих ссылок
    public override void OnStopAuthority()
    {
        weaponSwitcher.OnWeaponChanged -= LocalWeaponChanged;
    }

    // Called locally when player switches weapon, sends request to server
    // Вызывается локально при смене оружия игроком, отправляет запрос на сервер
    private void LocalWeaponChanged(int index)
    {
        CmdChangeWeapon(index);
    }

    // Command sent to server to update authoritative weapon index
    // Команда отправляется на сервер для обновления авторитетного индекса оружия
    [Command]
    private void CmdChangeWeapon(int index)
    {
        currentWeaponIndex = index;
    }

    // Mirror hook triggered when weapon indexSyncVar updates across the network
    // Хук Mirror, вызываемый при изменении SyncVar-индекса оружия по сети
    private void OnWeaponChanged(int oldIndex, int newIndex)
    {
        // Local owner already switched weapons instantly with zero latency
        // Владелец уже переключил оружие локально без сетевой задержки, пропускаем
        if (isOwned)
            return;

        // Apply weapon switch on remote client representation
        // Применяем смену оружия на визуальной модели чужого игрока
        weaponSwitcher.SetWeapon(newIndex, false);
    }
}