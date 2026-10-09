using System;
using System.Collections.Generic;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Inventory
{
    public enum EquipSlot
    {
        None,
        Flashlight,
        Pistol,
        SMG,
        FirstAid,
        Battery
    }

    /// <summary>
    /// Master Inventory Manager handling player item collection, stacking,
    /// equipping/holding in 3D hands, consumption, and synchronisation with HUD.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager instance;
        public static InventoryManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<InventoryManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("InventoryManager");
                        instance = go.AddComponent<InventoryManager>();
                    }
                }
                return instance;
            }
        }

        [Header("Inventory State")]
        [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();
        [SerializeField] private EquipSlot currentEquippedSlot = EquipSlot.Flashlight;

        [Header("3D Handheld Models on Player Camera")]
        [SerializeField] private GameObject heldFlashlightObject;
        [SerializeField] private GameObject heldPistolObject;
        [SerializeField] private GameObject heldSMGObject;
        [SerializeField] private GameObject heldFirstAidObject;
        [SerializeField] private GameObject heldBatteryObject;

        [Header("Default Sprite Icons")]
        [SerializeField] private Sprite iconFlashlight;
        [SerializeField] private Sprite iconBattery;
        [SerializeField] private Sprite iconPistol;
        [SerializeField] private Sprite iconAmmoHandgun;
        [SerializeField] private Sprite iconSMG;
        [SerializeField] private Sprite iconFirstAid;
        [SerializeField] private Sprite iconWater;
        [SerializeField] private Sprite iconPills;
        [SerializeField] private Sprite iconKeycard;
        [SerializeField] private Sprite iconNote;

        // Player component references
        private FirstPersonController playerController;
        private FlashlightController flashlightController;
        private PlayerCombat playerCombat;
        private PlayerHealth playerHealth;
        private PlayerSanity playerSanity;

        public List<InventoryItem> Items => items;
        public EquipSlot CurrentEquippedSlot => currentEquippedSlot;

        public event Action OnInventoryChanged;
        public event Action<EquipSlot> OnEquipChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            LoadDefaultIcons();
        }

        private void Start()
        {
            FindPlayerReferences();

            // Flashlight starts in inventory and is equipped by default
            if (GetItem(ItemType.Flashlight) == null)
            {
                AddItem(ItemType.Flashlight, "Heavy Flashlight", "Battery-powered handheld searchlight. Crucial for traversing dark sectors.", iconFlashlight, 1, true, false);
            }

            // Sync with PlayerCombat in case Pistol / SMG was already unlocked
            if (playerCombat != null)
            {
                if (playerCombat.HasUnlockedPistol && GetItem(ItemType.Pistol) == null)
                {
                    AddItem(ItemType.Pistol, "9mm Army Pistol", "Compact semi-automatic sidearm. Emergency defense against entities.", iconPistol, 1, true, false);
                }
                if (playerCombat.HasUnlockedSMG && GetItem(ItemType.SMG) == null)
                {
                    AddItem(ItemType.SMG, "Tactical Submachine Gun", "Rapid-fire automatic weapon for entity defense.", iconSMG, 1, true, false);
                }
            }

            EquipItem(EquipSlot.Flashlight, false);
        }

        private void Update()
        {
            HandleQuickSlotInput();
        }

        private void HandleQuickSlotInput()
        {
            // Number keys 1-4 for quick selection
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (HasItem(ItemType.Flashlight)) EquipItem(EquipSlot.Flashlight);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (HasItem(ItemType.Pistol)) EquipItem(EquipSlot.Pistol);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (HasItem(ItemType.SMG)) EquipItem(EquipSlot.SMG);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                if (HasItem(ItemType.FirstAid)) EquipItem(EquipSlot.FirstAid);
                else if (HasItem(ItemType.Battery)) EquipItem(EquipSlot.Battery);
            }
        }

        private void FindPlayerReferences()
        {
            playerController = FindFirstObjectByType<FirstPersonController>();
            flashlightController = FindFirstObjectByType<FlashlightController>();
            playerCombat = FindFirstObjectByType<PlayerCombat>();
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerSanity = FindFirstObjectByType<PlayerSanity>();

            // Auto-locate handheld weapon/tool models if unassigned
            if (Camera.main != null)
            {
                Transform camT = Camera.main.transform;
                if (heldFlashlightObject == null)
                {
                    Transform t = camT.Find("Flashlight") ?? camT.Find("HandheldFlashlight");
                    if (t != null) heldFlashlightObject = t.gameObject;
                }
                if (heldPistolObject == null)
                {
                    Transform t = camT.Find("DefenseHandgun/Model_ArmyPistol") ?? camT.Find("DefenseHandgun");
                    if (t != null) heldPistolObject = t.gameObject;
                }
                if (heldSMGObject == null)
                {
                    Transform t = camT.Find("DefenseHandgun/Model_TacticalSMG");
                    if (t != null) heldSMGObject = t.gameObject;
                }
            }
        }

        private void LoadDefaultIcons()
        {
            const string iconRoot = "Assets/PSXHorrorUIFree/Sprites/icons";
            if (iconFlashlight == null) iconFlashlight = Resources.Load<Sprite>("flashlight") ?? LoadSpriteAtPath($"{iconRoot}/large/flashlight.png") ?? LoadSpriteAtPath($"{iconRoot}/flashlight.png");
            if (iconBattery == null) iconBattery = LoadSpriteAtPath($"{iconRoot}/flashlight.png");
            if (iconPistol == null) iconPistol = LoadSpriteAtPath($"{iconRoot}/large/pistol.png") ?? LoadSpriteAtPath($"{iconRoot}/pistol.png");
            if (iconAmmoHandgun == null) iconAmmoHandgun = LoadSpriteAtPath($"{iconRoot}/large/ammo_handgun.png") ?? LoadSpriteAtPath($"{iconRoot}/ammo_handgun.png");
            if (iconSMG == null) iconSMG = LoadSpriteAtPath($"{iconRoot}/large/pistol.png") ?? LoadSpriteAtPath($"{iconRoot}/pistol.png");
            if (iconFirstAid == null) iconFirstAid = LoadSpriteAtPath($"{iconRoot}/large/first_aid_kit.png") ?? LoadSpriteAtPath($"{iconRoot}/first_aid_kit.png");
            if (iconWater == null) iconWater = LoadSpriteAtPath($"{iconRoot}/herb.png");
            if (iconPills == null) iconPills = LoadSpriteAtPath($"{iconRoot}/ink_ribbon.png");
            if (iconKeycard == null) iconKeycard = LoadSpriteAtPath($"{iconRoot}/large/key_skeleton.png") ?? LoadSpriteAtPath($"{iconRoot}/key_skeleton.png");
            if (iconNote == null) iconNote = LoadSpriteAtPath($"{iconRoot}/large/note.png") ?? LoadSpriteAtPath($"{iconRoot}/note.png");

            if (HUDManager.Instance != null)
            {
                if (iconPistol == null) iconPistol = HUDManager.Instance.PistolIcon;
            }
        }

        private Sprite LoadSpriteAtPath(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }

        public void AddItem(ItemType type, string name, string desc, Sprite icon = null, int quantity = 1, bool isEquippable = false, bool isConsumable = false)
        {
            if (icon == null) icon = GetDefaultIcon(type);

            InventoryItem existing = GetItem(type);
            if (existing != null)
            {
                existing.quantity += quantity;
            }
            else
            {
                InventoryItem newItem = new InventoryItem(type, name, desc, icon, quantity, isEquippable, isConsumable);
                items.Add(newItem);
            }

            UpdateItemStatuses();
            OnInventoryChanged?.Invoke();
        }

        public bool RemoveItem(ItemType type, int quantity = 1)
        {
            InventoryItem item = GetItem(type);
            if (item == null) return false;

            item.quantity -= quantity;
            if (item.quantity <= 0)
            {
                items.Remove(item);
                if (IsItemEquipped(type))
                {
                    EquipItem(EquipSlot.Flashlight);
                }
            }

            UpdateItemStatuses();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool HasItem(ItemType type)
        {
            InventoryItem item = GetItem(type);
            return item != null && item.quantity > 0;
        }

        public InventoryItem GetItem(ItemType type)
        {
            return items.Find(i => i.itemType == type);
        }

        public Sprite GetDefaultIcon(ItemType type)
        {
            switch (type)
            {
                case ItemType.Flashlight: return iconFlashlight;
                case ItemType.Battery: return iconBattery;
                case ItemType.Pistol: return iconPistol;
                case ItemType.PistolAmmo: return iconAmmoHandgun;
                case ItemType.SMG: return iconSMG != null ? iconSMG : iconPistol;
                case ItemType.SMGAmmo: return iconAmmoHandgun;
                case ItemType.FirstAid: return iconFirstAid;
                case ItemType.AlmondWater: return iconWater;
                case ItemType.SanityPills: return iconPills;
                case ItemType.Keycard: return iconKeycard;
                case ItemType.Note: return iconNote;
                default: return iconFlashlight;
            }
        }

        public void UseItem(ItemType type)
        {
            InventoryItem item = GetItem(type);
            if (item == null || item.quantity <= 0) return;

            switch (type)
            {
                case ItemType.Battery:
                    if (flashlightController != null)
                    {
                        flashlightController.Recharge(45f);
                        RemoveItem(type, 1);
                        PlayUseSound();
                        if (HUDManager.Instance != null)
                            HUDManager.Instance.ShowNotification("Used Battery (+45% Charge)");
                    }
                    break;

                case ItemType.FirstAid:
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(50f);
                        RemoveItem(type, 1);
                        PlayUseSound();
                        if (HUDManager.Instance != null)
                            HUDManager.Instance.ShowNotification("Used First Aid Kit (+50 HP)");
                    }
                    break;

                case ItemType.AlmondWater:
                    if (playerSanity != null)
                    {
                        playerSanity.RestoreSanity(40f);
                        RemoveItem(type, 1);
                        PlayUseSound();
                        if (HUDManager.Instance != null)
                            HUDManager.Instance.ShowNotification("Drank Almond Water (+40 Sanity)");
                    }
                    break;

                case ItemType.SanityPills:
                    if (playerSanity != null)
                    {
                        playerSanity.RestoreSanity(30f);
                        RemoveItem(type, 1);
                        PlayUseSound();
                        if (HUDManager.Instance != null)
                            HUDManager.Instance.ShowNotification("Took Calming Pills (+30 Sanity)");
                    }
                    break;

                case ItemType.Flashlight:
                    EquipItem(EquipSlot.Flashlight);
                    break;

                case ItemType.Pistol:
                    EquipItem(EquipSlot.Pistol);
                    break;

                case ItemType.SMG:
                    EquipItem(EquipSlot.SMG);
                    break;

                case ItemType.PistolAmmo:
                    if (playerCombat != null)
                    {
                        playerCombat.AddPistolAmmo(item.quantity * 6);
                        RemoveItem(type, item.quantity);
                        PlayUseSound();
                        if (HUDManager.Instance != null)
                            HUDManager.Instance.ShowNotification("Loaded Pistol Ammunition");
                    }
                    break;
            }

            UpdateItemStatuses();
        }

        public void EquipItem(EquipSlot slot, bool playSound = true)
        {
            if (slot == currentEquippedSlot && slot != EquipSlot.None)
            {
                // Already equipped
                return;
            }

            // Check requirements
            if (slot == EquipSlot.Pistol && !HasItem(ItemType.Pistol)) return;
            if (slot == EquipSlot.SMG && !HasItem(ItemType.SMG)) return;

            currentEquippedSlot = slot;

            ApplyEquippedVisuals();

            if (playSound)
            {
                PlayEquipSound();
            }

            // Synchronize with PlayerCombat
            if (playerCombat != null)
            {
                if (currentEquippedSlot == EquipSlot.Pistol)
                {
                    playerCombat.SetCombatActive(true);
                    playerCombat.SwitchWeapon(WeaponType.Pistol);
                }
                else if (currentEquippedSlot == EquipSlot.SMG)
                {
                    playerCombat.SetCombatActive(true);
                    playerCombat.SwitchWeapon(WeaponType.SubmachineGun);
                }
                else
                {
                    playerCombat.SetCombatActive(false);
                }
            }

            UpdateItemStatuses();
            OnEquipChanged?.Invoke(currentEquippedSlot);
            OnInventoryChanged?.Invoke();

            if (HUDManager.Instance != null && playSound)
            {
                string slotName = slot == EquipSlot.Flashlight ? "Flashlight"
                    : slot == EquipSlot.Pistol ? "9mm Army Pistol"
                    : slot == EquipSlot.SMG ? "Tactical SMG"
                    : slot == EquipSlot.FirstAid ? "First Aid Kit"
                    : slot.ToString();
                HUDManager.Instance.ShowNotification($"Equipped: {slotName}");
            }
        }

        private void ApplyEquippedVisuals()
        {
            if (playerCombat == null) FindPlayerReferences();

            // Toggle 3D handheld models
            if (heldFlashlightObject != null)
            {
                heldFlashlightObject.SetActive(currentEquippedSlot == EquipSlot.Flashlight);
            }

            if (heldPistolObject != null)
            {
                heldPistolObject.SetActive(currentEquippedSlot == EquipSlot.Pistol);
            }

            if (heldSMGObject != null)
            {
                heldSMGObject.SetActive(currentEquippedSlot == EquipSlot.SMG);
            }

            if (heldFirstAidObject != null)
            {
                heldFirstAidObject.SetActive(currentEquippedSlot == EquipSlot.FirstAid);
            }

            if (heldBatteryObject != null)
            {
                heldBatteryObject.SetActive(currentEquippedSlot == EquipSlot.Battery);
            }
        }

        private bool IsItemEquipped(ItemType type)
        {
            if (type == ItemType.Flashlight && currentEquippedSlot == EquipSlot.Flashlight) return true;
            if (type == ItemType.Pistol && currentEquippedSlot == EquipSlot.Pistol) return true;
            if (type == ItemType.SMG && currentEquippedSlot == EquipSlot.SMG) return true;
            if (type == ItemType.FirstAid && currentEquippedSlot == EquipSlot.FirstAid) return true;
            if (type == ItemType.Battery && currentEquippedSlot == EquipSlot.Battery) return true;
            return false;
        }

        public void UpdateItemStatuses()
        {
            foreach (var item in items)
            {
                switch (item.itemType)
                {
                    case ItemType.Flashlight:
                        float bat = flashlightController != null ? flashlightController.CurrentBattery : 100f;
                        item.statusText = IsItemEquipped(item.itemType) ? $"EQUIPPED ({Mathf.RoundToInt(bat)}%)" : $"{Mathf.RoundToInt(bat)}%";
                        break;

                    case ItemType.Pistol:
                        int cur = playerCombat != null ? playerCombat.CurrentAmmo : 6;
                        int res = playerCombat != null ? playerCombat.ReserveAmmo : 6;
                        item.statusText = IsItemEquipped(item.itemType) ? $"EQUIPPED ({cur}/{res})" : $"{cur}/{res}";
                        break;

                    case ItemType.SMG:
                        int curS = playerCombat != null ? playerCombat.CurrentAmmo : 20;
                        int resS = playerCombat != null ? playerCombat.ReserveAmmo : 20;
                        item.statusText = IsItemEquipped(item.itemType) ? $"EQUIPPED ({curS}/{resS})" : $"{curS}/{resS}";
                        break;

                    default:
                        if (IsItemEquipped(item.itemType))
                            item.statusText = "EQUIPPED";
                        else
                            item.statusText = $"x{item.quantity}";
                        break;
                }
            }
        }

        private void PlayEquipSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.7f, 1.3f);
            }
        }

        private void PlayUseSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.flashlightClickClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.flashlightClickClip, 0.8f, 1.1f);
            }
        }

        public void SetHeldObjects(GameObject flashlight, GameObject pistol, GameObject smg, GameObject firstAid = null, GameObject battery = null)
        {
            heldFlashlightObject = flashlight;
            heldPistolObject = pistol;
            heldSMGObject = smg;
            heldFirstAidObject = firstAid;
            heldBatteryObject = battery;
            ApplyEquippedVisuals();
        }
    }
}
