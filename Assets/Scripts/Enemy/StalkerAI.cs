using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Enemy
{
    public enum StalkerState
    {
        Patrol,
        Investigate,
        Chase,
        Search,
        Attack,
        Stunned,
        Dead
    }

    /// <summary>
    /// Stalker AI using NavMeshAgent with multi-stage stealth detection,
    /// dynamic vision cone (flashlight sensitivity & crouch stealth),
    /// sound investigation, and combat reaction to player attacks.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class StalkerAI : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private StalkerState currentState = StalkerState.Patrol;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Health & Combat")]
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float attackDamage = 30.0f;
        [SerializeField] private float attackCooldown = 1.3f;
        [SerializeField] private float stunDuration = 2.2f;
        private float currentHealth;
        private float attackCooldownTimer;

        [Header("Movement Speeds")]
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float investigateSpeed = 3.0f;
        [SerializeField] private float chaseSpeed = 4.8f;

        [Header("Vision Cone & Detection")]
        [SerializeField] private float baseViewDistance = 12.0f;
        [SerializeField] private float flashlightViewDistance = 24.0f;
        [SerializeField] private float fieldOfViewAngle = 100.0f;
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask sightObstacles = ~0;

        [Header("Patrol Settings")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private float waypointWaitTime = 2.5f;
        [SerializeField] private float randomPatrolRadius = 15.0f;

        [Header("Combat & Kill")]
        [SerializeField] private float killDistance = 1.8f;
        [SerializeField] private float searchDuration = 4.5f;
        [SerializeField] private float lostSightGraceDuration = 4.0f;

        // Pacing & Behavioral Modifiers
        private bool isDormant = false;
        private bool isFleeing = false;

        // References
        private NavMeshAgent agent;
        private FirstPersonController playerController;
        private FlashlightController playerFlashlight;
        private Transform playerTransform;

        // State Tracking
        private int currentWaypointIndex;
        private float stateTimer;
        private float lostSightTimer;
        private Vector3 lastKnownPlayerPos;
        private Vector3 patrolDestination;
        private bool hasDetectedPlayer;
        private float soundReactionCooldown;

        public StalkerState CurrentState => currentState;
        public bool IsDormant => isDormant;
        public void SetAnimator(Animator anim) => animator = anim;

        private EntityProximityAudio entityAudio;

        private void Awake()
        {
            // Guarantee valid NavMesh exists in scene
            RuntimeNavMeshBaker.EnsureNavMesh();

            agent = GetComponent<NavMeshAgent>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            currentHealth = maxHealth;

            // Proximity vocalisations from the Backrooms Entity SFX pack
            entityAudio = GetComponent<EntityProximityAudio>();
            if (entityAudio == null) entityAudio = gameObject.AddComponent<EntityProximityAudio>();

            // Ensure Blackout Zone is attached to plunge nearby lights into darkness
            if (GetComponent<EntityBlackoutZone>() == null)
            {
                gameObject.AddComponent<EntityBlackoutZone>();
            }
        }

        /// <summary>
        /// Plays an entity vocalisation from the entity's position. Falls back to the
        /// old procedural clip if the Entity SFX pack isn't assigned on the AudioManager.
        /// </summary>
        private void PlayEntityVocal(float volume, AudioClip fallbackClip, bool fallbackIs2D, float fallbackRange = 20f)
        {
            if (entityAudio != null && AudioManager.Instance != null && AudioManager.Instance.HasEntityClips)
            {
                entityAudio.PlayReaction(volume);
                return;
            }

            if (AudioManager.Instance == null || fallbackClip == null) return;
            if (fallbackIs2D) AudioManager.Instance.Play2D(fallbackClip, volume);
            else AudioManager.Instance.PlayAtPosition(fallbackClip, transform.position, volume, fallbackRange);
        }

        private void Start()
        {
            FindPlayerReferences();
            EnsureOnNavMesh();
            SetState(StalkerState.Patrol);
        }

        private void EnsureOnNavMesh()
        {
            if (agent != null && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 25.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }
        }

        private void FindPlayerReferences()
        {
            playerController = FindFirstObjectByType<FirstPersonController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
                playerFlashlight = playerController.GetComponentInChildren<FlashlightController>();
            }
        }

        private void Update()
        {
            if (currentState == StalkerState.Dead) return;

            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            EnsureOnNavMesh();

            if (playerTransform == null)
            {
                FindPlayerReferences();
                if (playerTransform == null) return;
            }

            soundReactionCooldown -= Time.deltaTime;
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

            UpdateAnimator();

            CheckVision();

            switch (currentState)
            {
                case StalkerState.Patrol:
                    UpdatePatrolState();
                    break;
                case StalkerState.Investigate:
                    UpdateInvestigateState();
                    break;
                case StalkerState.Chase:
                    UpdateChaseState();
                    break;
                case StalkerState.Search:
                    UpdateSearchState();
                    break;
                case StalkerState.Stunned:
                    UpdateStunnedState();
                    break;
                case StalkerState.Attack:
                case StalkerState.Dead:
                    break;
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            if (agent != null && agent.isOnNavMesh)
            {
                float currentSpeed = agent.velocity.magnitude;
                float speedParam = 0f;
                if (currentSpeed > 0.1f)
                {
                    if (currentSpeed <= patrolSpeed)
                    {
                        speedParam = Mathf.Lerp(0f, 1f, currentSpeed / patrolSpeed);
                    }
                    else
                    {
                        speedParam = Mathf.Lerp(1f, 2f, Mathf.Clamp01((currentSpeed - patrolSpeed) / Mathf.Max(0.1f, chaseSpeed - patrolSpeed)));
                    }
                }
                animator.SetFloat("Speed", speedParam);
            }
        }

        private void FallbackKinematicMovement(Vector3 targetPosition, float speed)
        {
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist > 0.3f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6.0f);

                Vector3 moveStep = transform.forward * (speed * Time.deltaTime);
                Vector3 origin = transform.position + Vector3.up * 0.9f;

                if (!Physics.SphereCast(origin, 0.4f, transform.forward, out RaycastHit hit, moveStep.magnitude + 0.2f, sightObstacles, QueryTriggerInteraction.Ignore))
                {
                    transform.position += moveStep;
                }
                else
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                    transform.position += slideDir * (speed * 0.7f * Time.deltaTime);
                }

                if (animator != null)
                {
                    float speedParam = (speed > patrolSpeed + 0.1f) ? 2.0f : 1.0f;
                    animator.SetFloat("Speed", speedParam);
                }
            }
            else
            {
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }

        private void CheckVision()
        {
            if (isDormant || isFleeing || currentState == StalkerState.Attack || currentState == StalkerState.Stunned) return;

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = playerTransform.position + Vector3.up * (playerController.IsCrouching ? 0.4f : 1.2f);
            Vector3 dirToPlayer = targetPos - eyePos;
            float distToPlayer = dirToPlayer.magnitude;

            // Flashlight detection: entity immediately spots the bright beam down the corridor!
            if (playerFlashlight != null && playerFlashlight.IsOn && distToPlayer <= flashlightViewDistance)
            {
                lastKnownPlayerPos = playerTransform.position;
                lostSightTimer = lostSightGraceDuration;
                if (currentState != StalkerState.Chase)
                {
                    OnSpotPlayer();
                    return;
                }
            }

            // Proximity auditory / scent awareness: entity senses player around corridor corners
            float senseRadius = playerController != null && playerController.IsCrouching ? 5.0f : 12.0f;
            if (distToPlayer <= senseRadius)
            {
                lastKnownPlayerPos = playerTransform.position;
                lostSightTimer = lostSightGraceDuration;
                if (currentState != StalkerState.Chase)
                {
                    OnSpotPlayer();
                    return;
                }
            }

            // Compute effective detection range based on stealth & angle
            float maxRange = baseViewDistance;
            if (playerController != null && playerController.IsCrouching)
            {
                maxRange *= 0.55f;
            }

            if (distToPlayer <= maxRange)
            {
                float angle = Vector3.Angle(transform.forward, dirToPlayer);
                bool isVeryClose = distToPlayer < 4.5f;

                if (angle < fieldOfViewAngle * 0.5f || isVeryClose)
                {
                    // Raycast to check line of sight to player
                    bool hasDirectLOS = false;
                    if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, distToPlayer + 0.5f, sightObstacles, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform) || 
                            hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<FirstPersonController>() != null)
                        {
                            hasDirectLOS = true;
                        }
                    }
                    else
                    {
                        hasDirectLOS = true;
                    }

                    if (hasDirectLOS)
                    {
                        lastKnownPlayerPos = playerTransform.position;
                        lostSightTimer = lostSightGraceDuration;
                        if (currentState != StalkerState.Chase)
                        {
                            OnSpotPlayer();
                        }
                        return;
                    }
                }
            }

            // If we were chasing but lost direct LOS (e.g. player rounded a corner)
            if (currentState == StalkerState.Chase)
            {
                lostSightTimer -= Time.deltaTime;
                if (lostSightTimer > 0f)
                {
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.speed = chaseSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(playerTransform.position);
                    }
                    else
                    {
                        FallbackKinematicMovement(playerTransform.position, chaseSpeed);
                    }
                    return;
                }

                SetState(StalkerState.Search);
            }
        }

        private void OnSpotPlayer()
        {
            lostSightTimer = lostSightGraceDuration;
            SetState(StalkerState.Chase);
            PlayEntityVocal(1.0f, AudioManager.Instance != null ? AudioManager.Instance.monsterSpottedClip : null, true);
        }

        public void OnHearNoise(Vector3 noiseOrigin)
        {
            if (isDormant || isFleeing || currentState == StalkerState.Attack || currentState == StalkerState.Stunned) return;

            if (currentState == StalkerState.Chase)
            {
                lastKnownPlayerPos = noiseOrigin;
                lostSightTimer = Mathf.Max(lostSightTimer, 3.5f);
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.SetDestination(playerTransform.position);
                }
                return;
            }

            if (soundReactionCooldown > 0f) return;
            soundReactionCooldown = 2.0f;
            lastKnownPlayerPos = noiseOrigin;

            float dist = Vector3.Distance(transform.position, noiseOrigin);
            if (dist < 8.0f)
            {
                lostSightTimer = lostSightGraceDuration;
                SetState(StalkerState.Chase);
                PlayEntityVocal(1.0f, AudioManager.Instance != null ? AudioManager.Instance.monsterSpottedClip : null, true);
            }
            else
            {
                SetState(StalkerState.Investigate);
                PlayEntityVocal(0.6f, AudioManager.Instance != null ? AudioManager.Instance.monsterGrowlClip : null, false);
            }
        }

        private void SetState(StalkerState newState)
        {
            currentState = newState;
            stateTimer = 0f;

            if (agent != null && agent.isOnNavMesh)
            {
                switch (newState)
                {
                    case StalkerState.Patrol:
                        agent.speed = patrolSpeed;
                        agent.isStopped = false;
                        MoveToNextPatrolPoint();
                        break;

                    case StalkerState.Investigate:
                        agent.speed = investigateSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(lastKnownPlayerPos);
                        break;

                    case StalkerState.Chase:
                        agent.speed = chaseSpeed;
                        agent.isStopped = false;
                        if (playerTransform != null) agent.SetDestination(playerTransform.position);
                        break;

                    case StalkerState.Search:
                        agent.speed = patrolSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(lastKnownPlayerPos);
                        break;

                    case StalkerState.Attack:
                        agent.isStopped = true;
                        if (animator != null) animator.SetTrigger("Attack");
                        StartCoroutine(ExecuteKillSequence());
                        break;

                    case StalkerState.Stunned:
                        agent.isStopped = true;
                        break;

                    case StalkerState.Dead:
                        agent.isStopped = true;
                        break;
                }
            }
            else
            {
                switch (newState)
                {
                    case StalkerState.Patrol:
                        MoveToNextPatrolPoint();
                        break;
                    case StalkerState.Attack:
                        StartCoroutine(ExecuteKillSequence());
                        break;
                }
            }
        }

        private void UpdatePatrolState()
        {
            stateTimer += Time.deltaTime;
            bool reached = false;

            if (agent != null && agent.isOnNavMesh)
            {
                reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
            }
            else
            {
                FallbackKinematicMovement(patrolDestination, patrolSpeed);
                reached = Vector3.Distance(transform.position, patrolDestination) <= 1.5f;
            }

            // Stalk towards next area upon reaching destination or 6 second corridor hunt timeout
            if (reached || stateTimer >= 6.0f)
            {
                stateTimer = 0f;
                MoveToNextPatrolPoint();
            }
        }

        private void MoveToNextPatrolPoint()
        {
            // Actively hunt in the player's direction through the Backrooms maze!
            if (playerTransform != null)
            {
                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                Vector3 huntTarget = transform.position + dirToPlayer * Random.Range(10f, 20f) + Random.insideUnitSphere * 3f;
                huntTarget.y = transform.position.y;
                patrolDestination = huntTarget;

                if (agent != null && agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(huntTarget, out NavMeshHit huntHit, 16f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(huntHit.position);
                        patrolDestination = huntHit.position;
                        return;
                    }
                    else if (NavMesh.SamplePosition(playerTransform.position, out NavMeshHit pHit, 16f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(pHit.position);
                        patrolDestination = pHit.position;
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (waypoints.Count > 0)
            {
                Transform wp = waypoints[currentWaypointIndex];
                if (wp != null)
                {
                    patrolDestination = wp.position;
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.SetDestination(wp.position);
                    }
                }
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            }
            else
            {
                Vector3 randomDirection = Random.insideUnitSphere * randomPatrolRadius;
                randomDirection += transform.position;
                randomDirection.y = transform.position.y;
                patrolDestination = randomDirection;

                if (agent != null && agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, randomPatrolRadius, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                        patrolDestination = hit.position;
                    }
                }
            }
        }

        private void UpdateInvestigateState()
        {
            stateTimer += Time.deltaTime;
            bool reached = false;

            if (agent != null && agent.isOnNavMesh)
            {
                reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
            }
            else
            {
                FallbackKinematicMovement(lastKnownPlayerPos, investigateSpeed);
                reached = Vector3.Distance(transform.position, lastKnownPlayerPos) <= 1.5f;
            }

            if (reached || stateTimer >= 5.0f)
            {
                transform.Rotate(Vector3.up * (45f * Time.deltaTime));
                if (stateTimer >= 2.5f)
                {
                    SetState(StalkerState.Patrol);
                }
            }
        }

        private void UpdateChaseState()
        {
            if (playerTransform == null) return;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = chaseSpeed;
                agent.isStopped = false;
                agent.SetDestination(playerTransform.position);
            }
            else
            {
                FallbackKinematicMovement(playerTransform.position, chaseSpeed);
            }
            lastKnownPlayerPos = playerTransform.position;

            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= killDistance)
            {
                // Caught! Trigger immediate horrifying jumpscare!
                SetState(StalkerState.Attack);
            }
        }

        private void UpdateStunnedState()
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            stateTimer += Time.deltaTime;
            // Shake/twitch in place from gunshot shock
            transform.Rotate(Vector3.up * (Mathf.Sin(Time.time * 25f) * 15f * Time.deltaTime));

            if (stateTimer >= stunDuration)
            {
                StartCoroutine(FleeFromPlayerRoutine(16.0f));
            }
        }

        private IEnumerator FleeFromPlayerRoutine(float duration)
        {
            isFleeing = true;
            currentState = StalkerState.Patrol;

            // Pick destination pointing directly away from the player
            Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
            Vector3 awayDir = (transform.position - playerPos).normalized;
            Vector3 fleeDest = transform.position + awayDir * 35.0f;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = chaseSpeed * 1.15f;
                agent.isStopped = false;
                if (NavMesh.SamplePosition(fleeDest, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    agent.SetDestination(fleeDest);
                }
            }

            yield return new WaitForSeconds(duration);

            isFleeing = false;
            SetState(StalkerState.Patrol);
        }

        public void SetDormant(bool dormant)
        {
            isDormant = dormant;
            if (isDormant && currentState == StalkerState.Chase)
            {
                SetState(StalkerState.Patrol);
            }
        }

        public void SetAggressiveMode(bool aggressive)
        {
            if (aggressive)
            {
                patrolSpeed = 2.4f;
                chaseSpeed = 5.0f;
                baseViewDistance = 14.0f;
            }
        }

        public void TakeDamage(float damage, Vector3 knockbackDir)
        {
            if (currentState == StalkerState.Dead) return;

            currentHealth -= damage;

            // Pushback
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Move(knockbackDir * 2.0f);
            }

            if (currentHealth <= 0f)
            {
                Die();
                return;
            }

            if (animator != null)
            {
                animator.SetTrigger("Stun");
            }

            SetState(StalkerState.Stunned);

            PlayEntityVocal(1.0f, AudioManager.Instance != null ? AudioManager.Instance.monsterGrowlClip : null, false);

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("Entity Shot! Stunned & Retreating into corridors!");
            }
        }

        private void Die()
        {
            currentState = StalkerState.Dead;
            if (agent != null) agent.isStopped = true;
            if (animator != null) animator.SetTrigger("Die");

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("Entity Banished! Area is safe.");
            }

            if (AudioManager.Instance != null)
            {
                // Detached source so the death cry isn't cut off when this object is destroyed
                AudioClip deathClip = AudioManager.Instance.HasEntityClips
                    ? AudioManager.Instance.GetRandomEntityClip()
                    : AudioManager.Instance.monsterGrowlClip;
                if (entityAudio != null) entityAudio.enabled = false;
                AudioManager.Instance.PlayAtPosition(deathClip, transform.position, 1.0f, 25f);
            }

            Destroy(gameObject, 2.5f);
        }

        private void UpdateSearchState()
        {
            stateTimer += Time.deltaTime;
            bool reached = false;

            if (agent != null && agent.isOnNavMesh)
            {
                reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
            }
            else
            {
                FallbackKinematicMovement(lastKnownPlayerPos, patrolSpeed);
                reached = Vector3.Distance(transform.position, lastKnownPlayerPos) <= 1.5f;
            }

            if (reached || stateTimer >= searchDuration)
            {
                transform.Rotate(Vector3.up * (40f * Time.deltaTime));
                if (stateTimer >= searchDuration)
                {
                    SetState(StalkerState.Patrol);
                }
            }
        }

        private IEnumerator ExecuteKillSequence()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // 1. Immediately disable player input and lock movement
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // 2. Warp monster directly in front of the player's camera at eye level (~0.85m)
            Transform camTransform = Camera.main != null ? Camera.main.transform : null;
            if (camTransform != null)
            {
                Vector3 forward = camTransform.forward;
                forward.y = 0f;
                forward.Normalize();
                if (forward.sqrMagnitude < 0.01f) forward = transform.forward;

                Vector3 screamerPos = camTransform.position + forward * 0.85f;
                screamerPos.y = playerTransform != null ? playerTransform.position.y : transform.position.y;

                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(screamerPos);
                }
                else
                {
                    transform.position = screamerPos;
                }

                // Face the player camera dead-on
                transform.rotation = Quaternion.LookRotation((camTransform.position - screamerPos).normalized);
            }

            // 3. Trigger attack and scream animation
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }

            // 4. Play loud terrifying jumpscare screeches
            if (AudioManager.Instance != null)
            {
                if (AudioManager.Instance.jumpscareClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.jumpscareClip, 1.0f);
                }
                if (AudioManager.Instance.monsterSpottedClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.monsterSpottedClip, 1.0f);
                }
            }

            // 5. Trigger horror screen effects on HUD
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerDamageFlash();
            }

            // 6. Violent camera screamer lock & violent shake jitter
            float duration = 1.6f;
            float elapsed = 0f;
            Vector3 originalCamPos = camTransform != null ? camTransform.localPosition : Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (camTransform != null)
                {
                    Vector3 monsterFacePos = transform.position + Vector3.up * 1.55f;
                    Quaternion lookRot = Quaternion.LookRotation((monsterFacePos - camTransform.position).normalized);
                    camTransform.rotation = Quaternion.Slerp(camTransform.rotation, lookRot, Time.deltaTime * 30f);

                    // Violent screen jitter
                    float shakeAmt = Mathf.Lerp(0.09f, 0.02f, elapsed / duration);
                    Vector3 jitter = new Vector3(
                        Random.Range(-shakeAmt, shakeAmt),
                        Random.Range(-shakeAmt, shakeAmt),
                        Random.Range(-shakeAmt, shakeAmt)
                    );
                    camTransform.localPosition = originalCamPos + jitter;
                }
                yield return null;
            }

            if (camTransform != null)
            {
                camTransform.localPosition = originalCamPos;
            }

            // 7. Trigger Game Over
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }
        }

        public void AddWaypoint(Transform wp)
        {
            if (wp != null && !waypoints.Contains(wp))
            {
                waypoints.Add(wp);
            }
        }
    }
}
