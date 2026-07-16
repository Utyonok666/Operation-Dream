using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
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

    // Для мультиплеера
    public event System.Action<int> OnWeaponChanged;

    private readonly GameObject[] weapons = new GameObject[5];

    private void Awake()
    {
        weapons[0] = assaultRifle;
        weapons[1] = smg;
        weapons[2] = shotgun;
        weapons[3] = sniper;
        weapons[4] = pistol;
    }

    private void Start()
    {
        SetClass(assaultRifle);
    }

    private void Update()
    {
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

        GameObject weapon = weapons[index];

        if (weapon == null)
            return;

        // Если выбрали основное оружие — запоминаем его
        if (index != 4)
            currentPrimaryWeapon = weapon;

        EquipWeapon(weapon);

        if (notify)
            OnWeaponChanged?.Invoke(index);
    }

    private void SetClass(GameObject primaryClassWeapon)
    {
        if (primaryClassWeapon == null)
            return;

        currentPrimaryWeapon = primaryClassWeapon;

        EquipWeapon(currentPrimaryWeapon);
    }

    private void EquipWeapon(GameObject weaponToEquip)
    {
        if (weaponToEquip == null)
            return;

        if (pistol != null) pistol.SetActive(false);
        if (assaultRifle != null) assaultRifle.SetActive(false);
        if (smg != null) smg.SetActive(false);
        if (shotgun != null) shotgun.SetActive(false);
        if (sniper != null) sniper.SetActive(false);

        activeWeapon = weaponToEquip;
        activeWeapon.SetActive(true);

        activeWeaponController = activeWeapon.GetComponent<WeaponController>();
    }
}