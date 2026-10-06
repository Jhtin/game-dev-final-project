using UnityEngine;
using HorrorEscape.Audio;

namespace HorrorEscape.Player
{
    /// <summary>
    /// Smooth First-Person Controller handling movement, sprint with stamina,
    /// smooth crouching, head bobbing, footstep sounds, and noise emission for enemy AI.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.2f;
        [SerializeField] private float crouchSpeed = 1.8f;
        [SerializeField] private float gravity = 20.0f;

        [Header("Look Settings")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float maxPitch = 85.0f;
        [SerializeField] private float minPitch = -85.0f;

        [Header("Character Model & Animations")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private bool enableThirdPersonToggle = true;
        [SerializeField] private KeyCode togglePerspectiveKey = KeyCode.V;
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.35f, 1.45f, -2.2f);
        private bool isThirdPerson = false;
        public bool IsThirdPerson => isThirdPerson;

        [Header("Stamina System")]
        [SerializeField] private float maxStamina = 100.0f;
        [SerializeField] private float staminaDrainRate = 22.0f;
        [SerializeField] private float staminaRecoveryRate = 15.0f;
        [SerializeField] private float staminaCooldown = 1.2f;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float standingCamY = 0.75f;
        [SerializeField] private float crouchCamY = 0.2f;
        [SerializeField] private float crouchTransitionSpeed = 8.0f;

        [Header("Head Bobbing")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobFrequency = 1.5f;
        [SerializeField] private float bobAmount = 0.04f;

        [Header("Footsteps & Audio")]
        [SerializeField] private float walkStepInterval = 0.55f;
        [SerializeField] private float sprintStepInterval = 0.35f;
        [SerializeField] private float crouchStepInterval = 0.8f;

        [Header("Enemy Noise Radius")]
        [SerializeField] private float sprintNoiseRadius = 14.0f;
        [SerializeField] private float walkNoiseRadius = 5.0f;

        // Components & State
        private CharacterController controller;
        private float pitch;
        private float currentStamina;
        private float staminaCooldownTimer;
        private bool isCrouching;
        private bool isSprinting;
        private Vector3 moveDirection;
        private float stepCycle;
        private float nextStepTime;
        private Vector3 defaultCameraPos;

        // Public Accessors for HUD & AI
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public bool IsMoving => controller.velocity.magnitude > 0.2f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            currentStamina = maxStamina;

            if (standingCamY < 1.0f)
            {
                standingCamY = 1.6f;
                crouchCamY = 0.9f;
            }

            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }

            if (playerCamera != null)
            {
                defaultCameraPos = playerCamera.localPosition;
            }
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void Update()
        {
            HandleMouseLook();
            HandleCrouch();
            HandlePerspectiveToggle();
            HandleStamina();
            HandleMovement();
            HandleHeadBob();
            HandleFootsteps();
            UpdateCharacterAnimations();
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Yaw rotates player body
            transform.Rotate(Vector3.up * mouseX);

            // Pitch rotates camera
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

            // Determine if sprint is permitted
            bool wantsToSprint = Input.GetKey(KeyCode.LeftShift) && vertical > 0.1f && !isCrouching;
            isSprinting = wantsToSprint && currentStamina > 0.05f && staminaCooldownTimer <= 0f;

            // Pick speed
            float targetSpeed = walkSpeed;
            if (isCrouching) targetSpeed = crouchSpeed;
            else if (isSprinting) targetSpeed = sprintSpeed;

            Vector3 worldMove = (transform.right * inputDir.x + transform.forward * inputDir.z) * targetSpeed;

            if (controller.isGrounded)
            {
                moveDirection.x = worldMove.x;
                moveDirection.z = worldMove.z;
                moveDirection.y = -1.0f; // Keep grounded firmly
            }
            else
            {
                moveDirection.x = worldMove.x;
                moveDirection.z = worldMove.z;
                moveDirection.y -= gravity * Time.deltaTime;
            }

            controller.Move(moveDirection * Time.deltaTime);

            // Noise emission for AI
            if (IsMoving)
            {
                float noiseRadius = isSprinting ? sprintNoiseRadius : (isCrouching ? 0f : walkNoiseRadius);
                if (noiseRadius > 0f)
                {
                    EmitNoise(transform.position, noiseRadius);
                }
            }
        }

        private void HandleCrouch()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            {
                isCrouching = !isCrouching;
            }

            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            if (playerCamera != null && !isThirdPerson)
            {
                float targetCamY = isCrouching ? crouchCamY : standingCamY;
                Vector3 camPos = playerCamera.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
                playerCamera.localPosition = camPos;
            }
        }

        private void HandleStamina()
        {
            if (isSprinting && IsMoving)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    staminaCooldownTimer = staminaCooldown; // Exhaustion cooldown
                }
            }
            else
            {
                if (staminaCooldownTimer > 0f)
                {
                    staminaCooldownTimer -= Time.deltaTime;
                }
                else if (currentStamina < maxStamina)
                {
                    currentStamina += staminaRecoveryRate * Time.deltaTime;
                    if (currentStamina > maxStamina) currentStamina = maxStamina;
                }
            }
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || playerCamera == null || !controller.isGrounded || isThirdPerson) return;

            if (IsMoving)
            {
                float speedMultiplier = isSprinting ? 1.4f : (isCrouching ? 0.7f : 1.0f);
                stepCycle += Time.deltaTime * bobFrequency * speedMultiplier * 8.0f;

                float bobOffset = Mathf.Sin(stepCycle) * bobAmount * (isSprinting ? 1.5f : 1.0f);
                Vector3 pos = playerCamera.localPosition;
                float baseY = isCrouching ? crouchCamY : standingCamY;
                pos.y = baseY + bobOffset;
                playerCamera.localPosition = pos;
            }
            else
            {
                stepCycle = 0f;
                Vector3 pos = playerCamera.localPosition;
                float baseY = isCrouching ? crouchCamY : standingCamY;
                pos.y = Mathf.Lerp(pos.y, baseY, Time.deltaTime * 6f);
                playerCamera.localPosition = pos;
            }
        }

        private void HandleFootsteps()
        {
            if (!controller.isGrounded || !IsMoving) return;

            float interval = isSprinting ? sprintStepInterval : (isCrouching ? crouchStepInterval : walkStepInterval);
            nextStepTime += Time.deltaTime;

            if (nextStepTime >= interval)
            {
                nextStepTime = 0f;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.footstepClip != null)
            {
                float vol = isCrouching ? 0.15f : (isSprinting ? 0.75f : 0.4f);
                float pitchMod = Random.Range(0.85f, 1.15f);
                AudioManager.Instance.Play2D(AudioManager.Instance.footstepClip, vol, pitchMod);
            }
        }

        private void EmitNoise(Vector3 origin, float radius)
        {
            // Inform nearby enemies that a noise occurred
            Collider[] hits = Physics.OverlapSphere(origin, radius);
            foreach (var hit in hits)
            {
                var stalker = hit.GetComponent<HorrorEscape.Enemy.StalkerAI>();
                if (stalker != null)
                {
                    stalker.OnHearNoise(origin);
                }
            }
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandlePerspectiveToggle()
        {
            if (!enableThirdPersonToggle || playerCamera == null) return;

            if (Input.GetKeyDown(togglePerspectiveKey))
            {
                isThirdPerson = !isThirdPerson;
            }

            if (isThirdPerson)
            {
                Vector3 targetPos = thirdPersonOffset;
                if (isCrouching)
                {
                    targetPos.y -= 0.5f;
                }
                playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetPos, Time.deltaTime * 10f);
            }
            else
            {
                // In first person, smoothly return camera X and Z towards 0
                Vector3 pos = playerCamera.localPosition;
                pos.x = Mathf.Lerp(pos.x, 0f, Time.deltaTime * 10f);
                pos.z = Mathf.Lerp(pos.z, 0f, Time.deltaTime * 10f);
                playerCamera.localPosition = pos;
            }
        }

        private void UpdateCharacterAnimations()
        {
            if (characterAnimator == null) return;

            // 0 = Idle, 1 = Walk, 2 = Sprint
            float targetSpeed = 0f;
            if (IsMoving)
            {
                targetSpeed = isSprinting ? 2.0f : 1.0f;
            }

            float currentSpeed = characterAnimator.GetFloat("Speed");
            float smoothSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 8f);

            characterAnimator.SetFloat("Speed", smoothSpeed);
            characterAnimator.SetBool("IsMoving", IsMoving);
            characterAnimator.SetBool("IsSprinting", isSprinting);
            characterAnimator.SetBool("IsCrouching", isCrouching);
            characterAnimator.SetBool("IsGrounded", controller.isGrounded);
        }

        public Animator CharacterAnimator => characterAnimator;

        public void SetCharacterAnimator(Animator anim)
        {
            characterAnimator = anim;
        }
    }
}
