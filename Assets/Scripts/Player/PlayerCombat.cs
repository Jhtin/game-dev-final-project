using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.UI;

namespace HorrorEscape.Player
{
    /// <summary>
    /// Controls the emergency defense gun and ammunition system.
    /// The player is given limited ammunition (starts with 6 / 6) to defend against
    /// the Backrooms entity in emergencies without turning the game into a fast-paced shooter.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Ammunition & Gun Stats")]
        [SerializeField] private int currentAmmo = 6;
        [SerializeField] private int maxClipAmmo = 6;
        [SerializeField] private int reserveAmmo = 6;
        [SerializeField] private float gunDamage = 50.0f;
        [SerializeField] private float gunRange = 35.0f;
        [SerializeField] private float fireCooldown = 0.5f;
        [SerializeField] private float reloadTime = 1.3f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Visual & Recoil")]
        [SerializeField] private Transform weaponTransform;
        [SerializeField] private Light muzzleFlashLight;
        [SerializeField] private float recoilDistance = 0.08f;
        [SerializeField] private float recoilAngle = 12.0f;
        [SerializeField] private float recoilRecoverySpeed = 14.0f;

        private float cooldownTimer;
        private bool isReloading;
        private Quaternion defaultWeaponRot;
        private Vector3 defaultWeaponPos;
        private Transform cameraTransform;

        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public int MaxClipAmmo => maxClipAmmo;
        public bool IsReloading => isReloading;

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

            // Left Mouse Button: Fire
            if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f && !isReloading)
            {
                if (Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0.01f)
                {
                    Fire();
                }
            }

            // R: Reload
            if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < maxClipAmmo && reserveAmmo > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        public void Fire()
        {
            if (currentAmmo <= 0)
            {
                // Dry fire click
                PlayDryFireSound();
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("OUT OF AMMO! Press [R] to reload if you have ammo.");
                }

                if (reserveAmmo > 0 && !isReloading)
                {
                    StartCoroutine(ReloadRoutine());
                }
                return;
            }

            currentAmmo--;
            cooldownTimer = fireCooldown;
            UpdateHUDAmmo();

            // Play gunshot sound
            PlayGunshotSound();

            // Recoil & Muzzle flash
            StartCoroutine(AnimateRecoilAndMuzzleFlash());

            // Raycast forward from camera center
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
                if (Physics.SphereCast(ray, 0.2f, out RaycastHit hit, gunRange, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    // Check if hit the Stalker AI
                    StalkerAI enemy = hit.collider.GetComponentInParent<StalkerAI>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(gunDamage, cameraTransform.forward);
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
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("Reloading...");
            }

            if (AudioManager.Instance != null && AudioManager.Instance.reloadClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.reloadClip, 0.8f);
            }

            // Tilt gun down during reload
            if (weaponTransform != null)
            {
                Quaternion reloadRot = defaultWeaponRot * Quaternion.Euler(20f, -10f, -15f);
                float elapsed = 0f;
                while (elapsed < reloadTime)
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
                yield return new WaitForSeconds(reloadTime);
            }

            int needed = maxClipAmmo - currentAmmo;
            int transfer = Mathf.Min(needed, reserveAmmo);
            currentAmmo += transfer;
            reserveAmmo -= transfer;

            isReloading = false;
            UpdateHUDAmmo();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Reloaded ({currentAmmo}/{maxClipAmmo})");
            }
        }

        public void AddAmmo(int amount)
        {
            reserveAmmo += amount;
            UpdateHUDAmmo();

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.8f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Collected 9mm Ammunition (+{amount} rounds)");
            }
        }

        private void UpdateHUDAmmo()
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateAmmoText(currentAmmo, reserveAmmo);
            }
        }

        private IEnumerator AnimateRecoilAndMuzzleFlash()
        {
            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = true;
            }

            if (weaponTransform != null)
            {
                Quaternion kickRot = defaultWeaponRot * Quaternion.Euler(-recoilAngle, Random.Range(-2f, 2f), Random.Range(-3f, 3f));
                Vector3 kickPos = defaultWeaponPos - new Vector3(0f, 0f, recoilDistance);

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
                AudioManager.Instance.Play2D(AudioManager.Instance.gunshotClip, 0.95f, Random.Range(0.96f, 1.04f));
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
            foreach (var hit in hits)
            {
                var stalker = hit.GetComponent<StalkerAI>();
                if (stalker != null)
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

        public void SetMuzzleFlashLight(Light light)
        {
            muzzleFlashLight = light;
            if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
        }
    }
}
