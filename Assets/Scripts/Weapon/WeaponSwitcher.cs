using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// ============================================================
// WeaponSwitcher
// Manages local weapon switching (primary/secondary), loadouts, and class selection.
// Менеджер локального переключения оружия (основное/второстепенное), выкладок и классов.
// Works alongside Mirror's NetworkIdentity to sync active weapons across multiplayer clients.
// Работает в связке с NetworkIdentity (Mirror) для синхронизации активной пушки в сетевой игре.
// ============================================================
public class WeaponSwitcher : MonoBehaviour
{
    // Mirror component reference for local player authority checks
    // Ссылка на компонент NetworkIdentity для проверки локального владения персонажем
    [SerializeField] private NetworkIdentity networkIdentity;

    [Header("Вторичное оружие (Пистолет)")]
    // Secondary sidearm GameObject reference / Ссылка на GameObject вторичного оружия (пистолета)
    [SerializeField] private GameObject pistol;

    [Header("Основные пушки классов")]
    // Primary class weapon GameObjects / Основные варианты оружия для разных классов
    [SerializeField] private GameObject assaultRifle;
    [SerializeField] private GameObject smg;
    [SerializeField] private GameObject shotgun;
    [SerializeField] private GameObject sniper;

    // Currently equipped primary class weapon / Текущее выбранное основное оружие
    private GameObject currentPrimaryWeapon;
    
    // Currently active visible weapon GameObject / Активный в данный момент GameObject оружия
    private GameObject activeWeapon;
    
    // Cached controller component of the active weapon / Кэшированный скрипт управления активным оружием
    private WeaponController activeWeaponController;

    // Public accessors / Публичные свойства для доступа к активному оружию и пистолету
    public WeaponController ActiveWeapon => activeWeaponController;
    public GameObject PistolWeapon => pistol;

    // Event fired when active weapon index changes (used by NetworkWeaponSwitcher to broadcast over network)
    // Событие смены оружия (используется NetworkWeaponSwitcher для сетевой синхронизации)
    public event System.Action<int> OnWeaponChanged;

    // Internal indexed map storing available weapon GameObjects
    // Массив для быстрого доступа к объектам оружия по индексу
    private readonly GameObject[] weapons = new GameObject[5];

    private void Awake()
    {
        // Cache NetworkIdentity component on initialization / Кешируем NetworkIdentity при инициализации
        networkIdentity = GetComponent<NetworkIdentity>();

        // Populate indexed weapons array (0-3: Primaries, 4: Secondary)
        // Заполняем массив пушек по индексам (0-3: основные, 4: пистолет)
        weapons[0] = assaultRifle;
        weapons[1] = smg;
        weapons[2] = shotgun;
        weapons[3] = sniper;
        weapons[4] = pistol;
    }

    private void Start()
    {
        // Skip default setup for remote non-owned network proxies to prevent race condition with OnStartClient()
        // Пропускаем дефолтную инициализацию для чужих сетевых игроков во избежание гонки состояния с OnStartClient()
        if (networkIdentity != null && !networkIdentity.isOwned)
            return;

        // Equip default primary class weapon (Assault Rifle) on start for local player
        // Выставляем дефолтный класс (штурмовая винтовка) при старте для локального игрока
        SetClass(assaultRifle);
    }

    private void Update()
    {
        // Execute switching logic strictly on local client owner
        // Обработка ввода и смены оружия выполняется только для владельца персонажа
        if (!networkIdentity.isOwned)
            return;

        // Note: F1-F4 class changing is disabled during gameplay; loadout is changed via death menu.
        // Примечание: Смена класса через F1-F4 во время игры отключена, класс выбирается в меню смерти.

        // ----------------------------------------------------
        // Select Primary Weapon (Key '1') / Выбор основного оружия (Клавиша '1')
        // ----------------------------------------------------
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (currentPrimaryWeapon == assaultRifle)
                SetWeapon(0);
            else if (currentPrimaryWeapon == smg)
                SetWeapon(1);
            else if (currentPrimaryWeapon == shotgun)
                SetWeapon(2);
            else if (currentPrimaryWeapon == sniper)
                SetWeapon(3);
        }

        // ----------------------------------------------------
        // Select Secondary Pistol (Key '2') / Выбор пистолета (Клавиша '2')
        // ----------------------------------------------------
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SetWeapon(4);
    }

    /// <summary>
    /// Index mapping / Перечень индексов:
    /// 0 - Assault Rifle / Штурмовая винтовка
    /// 1 - SMG / ПП
    /// 2 - Shotgun / Дробовик
    /// 3 - Sniper / Снайперка
    /// 4 - Pistol / Пистолет
    /// </summary>
    public void SetWeapon(int index)
    {
        SetWeapon(index, true);
    }

    // Equips weapon by index with optional network change notification
    // Экипирует оружие по индексу с опциональной отправкой сетевого уведомления
    public void SetWeapon(int index, bool notify)
    {
        // Bounds safety check / Проверка выходя за пределы массива
        if (index < 0 || index >= weapons.Length)
            return;

        // Equip secondary pistol / Экипировка пистолета
        if (index == 4)
        {
            EquipLoadout(pistol, notify ? 4 : -1);
            return;
        }

        // Equip selected primary weapon / Экипировка одного из основных вариантов оружия
        if (index >= 0 && index <= 3)
        {
            GameObject primaryWeapon = weapons[index];
            if (primaryWeapon == null)
                return;

            currentPrimaryWeapon = primaryWeapon;
            EquipLoadout(primaryWeapon, notify ? index : -1);
            return;
        }
    }

    // Set active class weapon by index / Установка класса оружия по индексу
    public void SetClass(int classIndex)
    {
        SetWeapon(classIndex, true);
    }

    /// <summary>
    /// Re-enables current active weapon GameObject.
    /// Used on player respawn after weapons were hidden (e.g., via SetActive(false) on death).
    /// Заново включает GameObject текущего оружия. Вызывается при респавне, если оружие было скрыто.
    /// </summary>
    public void ReapplyCurrentLoadout()
    {
        GameObject primary = currentPrimaryWeapon != null ? currentPrimaryWeapon : assaultRifle;
        EquipLoadout(primary, -1);
    }

    // Internal helper to set primary class reference and equip it silently
    // Внутренний метод для установки текущего основного оружия и его тихой экипировки
    private void SetClass(GameObject primaryClassWeapon)
    {
        if (primaryClassWeapon == null)
            return;

        currentPrimaryWeapon = primaryClassWeapon;
        EquipLoadout(primaryClassWeapon, -1);
    }

    // Disables all weapons, enables the target weapon, updates active references, and triggers change event.
    // Выключает все остальные пушки, активирует нужную, обновляет ссылки и вызывает событие смены.
    private void EquipLoadout(GameObject weaponToEquip, int notifyIndex)
    {
        if (weaponToEquip == null)
            return;

        // Deactivate all weapon objects in array / Деактивируем все пушки в массиве
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                weapons[i].SetActive(false);
        }

        // Activate requested weapon object / Активируем выбранный объект оружия
        weaponToEquip.SetActive(true);

        // Cache references to active weapon and its controller
        // Сохраняем ссылки на активный объект и его WeaponController
        activeWeapon = weaponToEquip;
        activeWeaponController = activeWeapon.GetComponent<WeaponController>();

        // Trigger change event if valid index supplied / Вызываем событие сетевого уведомления при необходимости
        if (notifyIndex >= 0)
            OnWeaponChanged?.Invoke(notifyIndex);
    }
}