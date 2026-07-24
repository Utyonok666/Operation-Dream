using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class WeaponSwitcher : MonoBehaviour
{

    
    [SerializeField] private NetworkIdentity networkIdentity;

    [Header("Вторичное оружие (Пистолет)")]
    [SerializeField] private GameObject pistol;

    [Header("Основные пушки классов")]
    [SerializeField] private GameObject assaultRifle;
    [SerializeField] private GameObject smg;
    [SerializeField] private GameObject shotgun;
    [SerializeField] private GameObject sniper;

    private GameObject currentPrimaryWeapon;
    private GameObject activeWeapon;
    private WeaponController activeWeaponController;

    public WeaponController ActiveWeapon => activeWeaponController;
    public GameObject PistolWeapon => pistol;

    // Для мультиплеера
    public event System.Action<int> OnWeaponChanged;

    private readonly GameObject[] weapons = new GameObject[5];

    private void Awake()
    {
        networkIdentity = GetComponent<NetworkIdentity>();

        weapons[0] = assaultRifle;
        weapons[1] = smg;
        weapons[2] = shotgun;
        weapons[3] = sniper;
        weapons[4] = pistol;
    }

    private void Start()
    {
        // Для чужих игроков в сети НЕ выставляем дефолт здесь -
        // это сделает NetworkWeaponSwitcher.OnStartClient() на основе
        // синхронизированного currentWeaponIndex. Если сделать это тут
        // безусловно, возникает гонка: Start() может выполниться ПОСЛЕ
        // OnStartClient() и затереть уже корректно применённое оружие
        // обратно на assaultRifle.
        if (networkIdentity != null && !networkIdentity.isOwned)
            return;

        SetClass(assaultRifle);
    }

    private void Update()
    {
        if (!networkIdentity.isOwned)
            return;

        // ---- Выбор класса ----
        if (Keyboard.current.f1Key.wasPressedThisFrame)
            SetWeapon(0);

        if (Keyboard.current.f2Key.wasPressedThisFrame)
            SetWeapon(1);

        if (Keyboard.current.f3Key.wasPressedThisFrame)
            SetWeapon(2);

        if (Keyboard.current.f4Key.wasPressedThisFrame)
            SetWeapon(3);

        // ---- Достать оружие ----
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

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SetWeapon(4);
    }

    /// <summary>
    /// index:
    /// 0 - Assault
    /// 1 - SMG
    /// 2 - Shotgun
    /// 3 - Sniper
    /// 4 - Pistol
    /// </summary>
    public void SetWeapon(int index)
    {
        SetWeapon(index, true);
    }

    public void SetWeapon(int index, bool notify)
    {
        if (index < 0 || index >= weapons.Length)
            return;

        if (index == 4)
        {
            EquipLoadout(pistol, true, notify ? 4 : -1);
            return;
        }

        if (index >= 0 && index <= 3)
        {
            GameObject primaryWeapon = weapons[index];
            if (primaryWeapon == null)
                return;

            currentPrimaryWeapon = primaryWeapon;
            EquipLoadout(primaryWeapon, true, notify ? index : -1);
            return;
        }
    }

    public void SetClass(int classIndex)
    {
        SetWeapon(classIndex, true);
    }

    private void SetClass(GameObject primaryClassWeapon)
    {
        if (primaryClassWeapon == null)
            return;

        currentPrimaryWeapon = primaryClassWeapon;
        EquipLoadout(primaryClassWeapon, true, -1);
    }

    private void EquipLoadout(GameObject primaryWeapon, bool includePistol, int notifyIndex)
    {
        if (primaryWeapon == null)
            return;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                weapons[i].SetActive(false);
        }

        primaryWeapon.SetActive(true);

        if (includePistol && pistol != null)
            pistol.SetActive(true);

        activeWeapon = primaryWeapon;
        activeWeaponController = activeWeapon.GetComponent<WeaponController>();

        if (notifyIndex >= 0)
            OnWeaponChanged?.Invoke(notifyIndex);
    }
}