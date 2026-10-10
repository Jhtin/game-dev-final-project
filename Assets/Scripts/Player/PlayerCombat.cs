using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Managers;
using HorrorEscape.UI;

namespace HorrorEscape.Player
{
    public enum WeaponType
    {
        Pistol,
        SubmachineGun
    }

    /// <summary>
    /// Controls the emergency defense weapons (Army Pistol and Tactical SMG) and ammunition system.
    /// Supports authentic Polygon Survival 3D firearms with weapon switching, distinctive rates of fire,
    /// recoil characteristics, and limited ammunition.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon Selection")]
        [SerializeField] private WeaponType currentWeapon = WeaponType.Pistol;
        [SerializeField] private bool hasUnlockedPistol = false;
        [SerializeField] private bool hasUnlockedSMG = false;
        [SerializeField] private bool isCombatActive = false;

        [Header("Pistol Configuration")]
        [SerializeField] private GameObject pistolObject;
        [SerializeField] private int pistolCurrentAmmo = 6;
        [SerializeField] private int pistolMaxClipAmmo = 6;
        [SerializeField] private int pistolReserveAmmo = 6;
        [SerializeField] private float pistolDamage = 50.0f;
        [SerializeField] private float pistolRange = 35.0f;
        [SerializeField] private float pistolFireCooldown = 0.45f;
        [SerializeField] private float pistolReloadTime = 1.3f;
        [SerializeField] private float pistolRecoilDistance = 0.08f;
        [SerializeField] private float pistolRecoilAngle = 12.0f;

        [Header("SMG Configuration")]
        [SerializeField] private GameObject smgObject;
        [SerializeField] private int smgCurrentAmmo = 20;
        [SerializeField] private int smgMaxClipAmmo = 20;
        [SerializeField] private int smgReserveAmmo = 20;
        [SerializeField] private float smgDamage = 26.0f;
        [SerializeField] private float smgRange = 32.0f;
        [SerializeField] private float smgFireCooldown = 0.12f; // ~500 RPM full-auto
        [SerializeField] private float smgReloadTime = 1.8f;
        [SerializeField] private float smgRecoilDistance = 0.05f;
        [SerializeField] private float smgRecoilAngle = 6.0f;

        [Header("Layer & Audio")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private Light muzzleFlashLight;
        [SerializeField] private float recoilRecoverySpeed = 16.0f;

        [Header("Legacy / Compatibility Field")]
        [SerializeField] private Transform weaponTransform;

        private float cooldownTimer;
        private bool isReloading;
        private Quaternion defaultWeaponRot;
        private Vector3 defaultWeaponPos;
        private Transform cameraTransform;

        // Public properties
        public WeaponType CurrentWeapon => currentWeapon;
        public bool HasUnlockedPistol => hasUnlockedPistol;
        public bool HasUnlockedSMG => hasUnlockedSMG;
        public bool IsCombatActive => isCombatActive;
        public bool IsReloading => isReloading;

        public int CurrentAmmo => currentWeapon == WeaponType.Pistol ? pistolCurrentAmmo : smgCurrentAmmo;
        public int ReserveAmmo => currentWeapon == WeaponType.Pistol ? pistolReserveAmmo : smgReserveAmmo;
        public int MaxClipAmmo => currentWeapon == WeaponType.Pistol ? pistolMaxClipAmmo : smgMaxClipAmmo;

        private void Awake()
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (weaponTransform != null)
            {
                defaultWeaponPos = weaponTransform.localPosition;
                defaultWeaponRot = weaponTransform.localRotation;
            }

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }

            ApplyWeaponVisuals();
        }

        private void Start()
        {
            UpdateHUDAmmo();
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            bool isUIBlocking = (HUDManager.Instance != null && (HUDManager.Instance.IsInventoryOpen || HUDManager.Instance.IsReadingNote || HUDManager.Instance.IsPaused)) ||
                                (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsVictory));

            FirstPersonController fpc = GetComponent<FirstPersonController>();
            if (isUIBlocking || (fpc != null && fpc.IsHiding))
            {
                return;
            }

            // Weapon switching: [1] for Pistol, [2] for SMG, or scroll wheel
            if (!isReloading)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && currentWeapon != WeaponType.Pistol)
                {
                    SwitchWeapon(WeaponType.Pistol);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) && hasUnlockedSMG && currentWeapon != WeaponType.SubmachineGun)
                {
                    SwitchWeapon(WeaponType.SubmachineGun);
                }

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (hasUnlockedSMG && Mathf.Abs(scroll) > 0.02f)
                {
                    WeaponType next = currentWeapon == WeaponType.Pistol ? WeaponType.SubmachineGun : WeaponType.Pistol;
                    SwitchWeapon(next);
                }
            }

            // Left Mouse Button: Fire
            // Only fire if combat is active and corresponding weapon is unlocked
            bool canFireCurrent = (currentWeapon == WeaponType.Pistol && hasUnlockedPistol) ||
                                  (currentWeapon == WeaponType.SubmachineGun && hasUnlockedSMG);

            bool fireRequested = currentWeapon == WeaponType.SubmachineGun ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);

            if (isCombatActive && canFireCurrent && fireRequested && cooldownTimer <= 0f && !isReloading)
            {
                if (Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0.01f)
                {
                    Fire();
                }
            }

            // R: Reload
            if (isCombatActive && canFireCurrent && Input.GetKeyDown(KeyCode.R) && !isReloading && CurrentAmmo < MaxClipAmmo && ReserveAmmo > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        public void SetCombatActive(bool active)
        {
            isCombatActive = active;
            ApplyWeaponVisuals();
            UpdateHUDAmmo();
        }

        public void SwitchWeapon(WeaponType newWeapon)
        {
            if (newWeapon == WeaponType.Pistol && !hasUnlockedPistol) return;
            if (newWeapon == WeaponType.SubmachineGun && !hasUnlockedSMG) return;
            if (isReloading) return;

            currentWeapon = newWeapon;
            isCombatActive = true;
            ApplyWeaponVisuals();
            UpdateHUDAmmo();

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.6f, 1.4f);
            }

            if (HUDManager.Instance != null)
            {
                string name = currentWeapon == WeaponType.Pistol ? "9mm Army Pistol" : "Tactical SMG";
                HUDManager.Instance.ShowNotification($"Equipped: {name}");
            }
        }

        public void UnlockPistol(int startingAmmo = 6, int startingReserve = 6)
        {
            hasUnlockedPistol = true;
            isCombatActive = true;
            pistolCurrentAmmo = Mathf.Max(pistolCurrentAmmo, startingAmmo);
            pistolReserveAmmo = Mathf.Max(pistolReserveAmmo, startingReserve);
            SwitchWeapon(WeaponType.Pistol);

            if (HorrorEscape.Inventory.InventoryManager.Instance != null && !HorrorEscape.Inventory.InventoryManager.Instance.HasItem(HorrorEscape.Inventory.ItemType.Pistol))
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.Pistol,
                    "9mm Army Pistol",
                    "Compact semi-automatic sidearm. Emergency defense against entities.",
                    null,
                    1,
                    true,
                    false
                );
                HorrorEscape.Inventory.InventoryManager.Instance.EquipItem(HorrorEscape.Inventory.EquipSlot.Pistol, false);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("NEW WEAPON ACQUIRED: 9mm Army Pistol! Emergency defense unlocked.");
            }

            if (EscapeMissionManager.Instance != null)
            {
                EscapeMissionManager.Instance.OnGunFound("9mm Army Pistol");
            }
        }

        public void UnlockSMG(int startingAmmo = 20, int startingReserve = 20)
        {
            hasUnlockedSMG = true;
            isCombatActive = true;
            smgCurrentAmmo = Mathf.Max(smgCurrentAmmo, startingAmmo);
            smgReserveAmmo = Mathf.Max(smgReserveAmmo, startingReserve);
            SwitchWeapon(WeaponType.SubmachineGun);

            if (HorrorEscape.Inventory.InventoryManager.Instance != null && !HorrorEscape.Inventory.InventoryManager.Instance.HasItem(HorrorEscape.Inventory.ItemType.SMG))
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.SMG,
                    "Tactical Submachine Gun",
                    "Rapid-fire automatic weapon for entity defense.",
                    null,
                    1,
                    true,
                    false
                );
                HorrorEscape.Inventory.InventoryManager.Instance.EquipItem(HorrorEscape.Inventory.EquipSlot.SMG, false);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("NEW WEAPON ACQUIRED: Tactical Submachine Gun! Press [1] / [2] to switch.");
            }

            if (EscapeMissionManager.Instance != null)
            {
                EscapeMissionManager.Instance.OnGunFound("Tactical SMG");
            }
        }

        private void ApplyWeaponVisuals()
        {
            if (pistolObject != null)
            {
                pistolObject.SetActive(isCombatActive && currentWeapon == WeaponType.Pistol && hasUnlockedPistol);
            }
            if (smgObject != null)
            {
                smgObject.SetActive(isCombatActive && currentWeapon == WeaponType.SubmachineGun && hasUnlockedSMG);
            }
        }

        public void Fire()
        {
            int current = CurrentAmmo;
            int reserve = ReserveAmmo;

            if (current <= 0)
            {
                // Dry fire click
                PlayDryFireSound();
                if (HUDManager.Instance != null && Input.GetMouseButtonDown(0))
                {
                    HUDManager.Instance.ShowNotification("OUT OF AMMO! Press [R] to reload.");
                }

                if (reserve > 0 && !isReloading)
                {
                    StartCoroutine(ReloadRoutine());
                }
                return;
            }

            // Consume ammo
            if (currentWeapon == WeaponType.Pistol)
            {
                pistolCurrentAmmo--;
                cooldownTimer = pistolFireCooldown;
            }
            else
            {
                smgCurrentAmmo--;
                cooldownTimer = smgFireCooldown;
            }

            UpdateHUDAmmo();

            // Play gunshot sound
            PlayGunshotSound();

            // Recoil & Muzzle flash
            float recoilDist = currentWeapon == WeaponType.Pistol ? pistolRecoilDistance : smgRecoilDistance;
            float recoilAng = currentWeapon == WeaponType.Pistol ? pistolRecoilAngle : smgRecoilAngle;
            StartCoroutine(AnimateRecoilAndMuzzleFlash(recoilDist, recoilAng));

            // Raycast forward from camera center
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                float range = currentWeapon == WeaponType.Pistol ? pistolRange : smgRange;
                float dmg = currentWeapon == WeaponType.Pistol ? pistolDamage : smgDamage;

                Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
                if (Physics.SphereCast(ray, 0.18f, out RaycastHit hit, range, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    StalkerAI enemy = hit.collider.GetComponentInParent<StalkerAI>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(dmg, cameraTransform.forward);
                        PlayHitSound(hit.point, true);
                    }
                    else
                    {
                        PlayHitSound(hit.point, false);
                    }
                }

                // Alert nearby enemy to gunfire noise
                EmitGunfireNoise(transform.position, 28.0f);
            }
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;
            float time = currentWeapon == WeaponType.Pistol ? pistolReloadTime : smgReloadTime;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("Reloading...");
            }

            if (AudioManager.Instance != null && AudioManager.Instance.reloadClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.reloadClip, 0.8f);
            }

            // Tilt weapon down during reload
            if (weaponTransform != null)
            {
                Quaternion reloadRot = defaultWeaponRot * Quaternion.Euler(20f, -10f, -15f);
                float elapsed = 0f;
                while (elapsed < time)
                {
                    elapsed += Time.deltaTime;
                    weaponTransform.localRotation = Quaternion.Slerp(weaponTransform.localRotation, reloadRot, Time.deltaTime * 6f);
                    yield return null;
                }
                weaponTransform.localRotation = defaultWeaponRot;
                weaponTransform.localPosition = defaultWeaponPos;
            }
            else
            {
                yield return new WaitForSeconds(time);
            }

            if (currentWeapon == WeaponType.Pistol)
            {
                int needed = pistolMaxClipAmmo - pistolCurrentAmmo;
                int transfer = Mathf.Min(needed, pistolReserveAmmo);
                pistolCurrentAmmo += transfer;
                pistolReserveAmmo -= transfer;
            }
            else
            {
                int needed = smgMaxClipAmmo - smgCurrentAmmo;
                int transfer = Mathf.Min(needed, smgReserveAmmo);
                smgCurrentAmmo += transfer;
                smgReserveAmmo -= transfer;
            }

            isReloading = false;
            UpdateHUDAmmo();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Reloaded ({CurrentAmmo}/{MaxClipAmmo})");
            }
        }

        public void AddAmmo(int amount)
        {
            if (currentWeapon == WeaponType.SubmachineGun)
            {
                smgReserveAmmo += amount * 2; // SMG uses ammo faster, gives double count
            }
            else
            {
                pistolReserveAmmo += amount;
            }

            UpdateHUDAmmo();

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.8f);
            }

            if (HUDManager.Instance != null)
            {
                string type = currentWeapon == WeaponType.SubmachineGun ? "SMG" : "9mm";
                int count = currentWeapon == WeaponType.SubmachineGun ? amount * 2 : amount;
                HUDManager.Instance.ShowNotification($"Collected {type} Ammunition (+{count} rounds)");
            }
        }

        public void AddPistolAmmo(int amount)
        {
            pistolReserveAmmo += amount;
            UpdateHUDAmmo();
        }

        public void AddSMGAmmo(int amount)
        {
            smgReserveAmmo += amount;
            UpdateHUDAmmo();
        }

        private void UpdateHUDAmmo()
        {
            if (HUDManager.Instance != null)
            {
                if (!isCombatActive || (currentWeapon == WeaponType.Pistol && !hasUnlockedPistol) || (currentWeapon == WeaponType.SubmachineGun && !hasUnlockedSMG))
                {
                    HUDManager.Instance.UpdateAmmoText(0, 0, "UNARMED", false);
                }
                else
                {
                    string weaponName = currentWeapon == WeaponType.SubmachineGun ? "TACTICAL SMG" : "9MM PISTOL";
                    HUDManager.Instance.UpdateAmmoText(CurrentAmmo, ReserveAmmo, weaponName, true);
                }
            }
        }

        private IEnumerator AnimateRecoilAndMuzzleFlash(float recoilDist, float recoilAng)
        {
            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = true;
            }

            if (weaponTransform != null)
            {
                Quaternion kickRot = defaultWeaponRot * Quaternion.Euler(-recoilAng, Random.Range(-2f, 2f), Random.Range(-3f, 3f));
                Vector3 kickPos = defaultWeaponPos - new Vector3(0f, 0f, recoilDist);

                weaponTransform.localRotation = kickRot;
                weaponTransform.localPosition = kickPos;

                yield return new WaitForSeconds(0.04f);

                if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * recoilRecoverySpeed;
                    weaponTransform.localRotation = Quaternion.Slerp(weaponTransform.localRotation, defaultWeaponRot, t);
                    weaponTransform.localPosition = Vector3.Lerp(weaponTransform.localPosition, defaultWeaponPos, t);
                    yield return null;
                }

                weaponTransform.localRotation = defaultWeaponRot;
                weaponTransform.localPosition = defaultWeaponPos;
            }
            else
            {
                yield return new WaitForSeconds(0.04f);
                if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
            }
        }

        private void PlayGunshotSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.gunshotClip != null)
            {
                float pitch = currentWeapon == WeaponType.SubmachineGun ? Random.Range(1.15f, 1.25f) : Random.Range(0.96f, 1.04f);
                AudioManager.Instance.Play2D(AudioManager.Instance.gunshotClip, 0.95f, pitch);
            }
        }

        private void PlayDryFireSound()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.dryFireClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.dryFireClip, 0.7f, 1.4f);
            }
        }

        private void PlayHitSound(Vector3 point, bool isEnemy)
        {
            if (AudioManager.Instance != null)
            {
                if (isEnemy && AudioManager.Instance.monsterGrowlClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.monsterGrowlClip, point, 0.85f, 20f);
                }
                else if (AudioManager.Instance.doorCloseClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.doorCloseClip, point, 0.6f, 15f);
                }
            }
        }

        private void EmitGunfireNoise(Vector3 origin, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(origin, radius);
            var notified = new System.Collections.Generic.HashSet<StalkerAI>();
            foreach (var hit in hits)
            {
                var stalker = hit.GetComponentInParent<StalkerAI>();
                if (stalker != null && notified.Add(stalker))
                {
                    stalker.OnHearNoise(origin);
                }
            }
        }

        public void SetWeaponTransform(Transform weapon)
        {
            weaponTransform = weapon;
            if (weaponTransform != null)
            {
                defaultWeaponPos = weaponTransform.localPosition;
                defaultWeaponRot = weaponTransform.localRotation;
            }
        }

        public void SetWeaponObjects(GameObject pistol, GameObject smg)
        {
            pistolObject = pistol;
            smgObject = smg;
            ApplyWeaponVisuals();
        }

        public void SetMuzzleFlashLight(Light light)
        {
            muzzleFlashLight = light;
            if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
        }
    }
}
