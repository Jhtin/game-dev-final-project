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
        private bool hasDetectedPlayer;
        private float soundReactionCooldown;

        public StalkerState CurrentState => currentState;
        public bool IsDormant => isDormant;
        public void SetAnimator(Animator anim) => animator = anim;

        private EntityProximityAudio entityAudio;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            currentHealth = maxHealth;

            // Proximity vocalisations from the Backrooms Entity SFX pack
            entityAudio = GetComponent<EntityProximityAudio>();
            if (entityAudio == null) entityAudio = gameObject.AddComponent<EntityProximityAudio>();
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
            if (agent != null && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }
            SetState(StalkerState.Patrol);
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
                agent.isStopped = true;
                return;
            }

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
            if (animator == null || agent == null) return;

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

        private void CheckVision()
        {
            if (isDormant || isFleeing || currentState == StalkerState.Attack || currentState == StalkerState.Stunned) return;

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = playerTransform.position + Vector3.up * (playerController.IsCrouching ? 0.4f : 1.2f);
            Vector3 dirToPlayer = targetPos - eyePos;
            float distToPlayer = dirToPlayer.magnitude;

            // Compute effective detection range based on stealth & flashlight
            float maxRange = baseViewDistance;
            if (playerFlashlight != null && playerFlashlight.IsOn)
            {
                maxRange = flashlightViewDistance;
            }
            if (playerController != null && playerController.IsCrouching)
            {
                maxRange *= 0.55f; // Stealth crouching reduces detection distance!
            }

            if (distToPlayer <= maxRange)
            {
                float angle = Vector3.Angle(transform.forward, dirToPlayer);
                // Also detect if player is close behind monster or around tight corridor corners (within 3.5m)
                bool isVeryClose = distToPlayer < 3.5f;

                if (angle < fieldOfViewAngle * 0.5f || isVeryClose)
                {
                    // Raycast to check line of sight
                    if (!Physics.Raycast(eyePos, dirToPlayer.normalized, distToPlayer, sightObstacles, QueryTriggerInteraction.Ignore))
                    {
                        // Direct Line of Sight!
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

            // Proximity awareness: if within 4.5m in tight corridors, monster detects player
            if (distToPlayer < 4.5f && !playerController.IsCrouching)
            {
                lastKnownPlayerPos = playerTransform.position;
                lostSightTimer = lostSightGraceDuration;
                if (currentState != StalkerState.Chase)
                {
                    OnSpotPlayer();
                    return;
                }
            }

            // If we were chasing but lost direct LOS (e.g. player rounded a corner)
            if (currentState == StalkerState.Chase)
            {
                lostSightTimer -= Time.deltaTime;
                // While grace duration remains active, KEEP PURSUING relentlessly!
                if (lostSightTimer > 0f)
                {
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.speed = chaseSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(playerTransform.position);
                    }
                    return;
                }

                // Grace duration expired: transition to search mode at last known spot
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

            // If already chasing, hearing noise updates the pursuit destination and resets grace timer
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
            // If sprint or gunshot noise is close (< 8m), break into a full chase immediately!
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
            else if (newState == StalkerState.Attack)
            {
                StartCoroutine(ExecuteKillSequence());
            }
        }

        private void UpdatePatrolState()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
            {
                stateTimer += Time.deltaTime;
                if (stateTimer >= waypointWaitTime)
                {
                    stateTimer = 0f;
                    MoveToNextPatrolPoint();
                }
            }
        }

        private void MoveToNextPatrolPoint()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            if (waypoints.Count > 0)
            {
                Transform wp = waypoints[currentWaypointIndex];
                if (wp != null)
                {
                    agent.SetDestination(wp.position);
                }
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            }
            else
            {
                // Roam randomly on NavMesh
                Vector3 randomDirection = Random.insideUnitSphere * randomPatrolRadius;
                randomDirection += transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, randomPatrolRadius, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }

        private void UpdateInvestigateState()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                stateTimer += Time.deltaTime;
                // Look around in place
                transform.Rotate(Vector3.up * (35f * Time.deltaTime));

                if (stateTimer >= 3.5f)
                {
                    SetState(StalkerState.Patrol);
                }
            }
        }

        private void UpdateChaseState()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(playerTransform.position);
            }
            lastKnownPlayerPos = playerTransform.position;

            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= killDistance)
            {
                if (attackCooldownTimer <= 0f)
                {
                    attackCooldownTimer = attackCooldown;
                    PlayerHealth pHealth = playerController != null ? playerController.GetComponent<PlayerHealth>() : null;
                    if (pHealth != null)
                    {
                        pHealth.TakeDamage(attackDamage);
                        if (pHealth.IsDead)
                        {
                            SetState(StalkerState.Attack);
                        }
                    }
                    else
                    {
                        SetState(StalkerState.Attack);
                    }
                }
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
            if (agent == null || !agent.isOnNavMesh) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                stateTimer += Time.deltaTime;
                transform.Rotate(Vector3.up * (40f * Time.deltaTime));

                if (stateTimer >= searchDuration)
                {
                    SetState(StalkerState.Patrol);
                }
            }
        }

        private IEnumerator ExecuteKillSequence()
        {
            // Face player towards monster violently (classic jumpscare!)
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // Play jumpscare scream
            if (AudioManager.Instance != null && AudioManager.Instance.jumpscareClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.jumpscareClip, 1.0f);
            }

            Transform camTransform = Camera.main != null ? Camera.main.transform : null;
            float t = 0f;
            Vector3 monsterHeadPos = transform.position + Vector3.up * eyeHeight;

            while (t < 0.8f)
            {
                t += Time.deltaTime;
                if (camTransform != null)
                {
                    Quaternion targetRot = Quaternion.LookRotation(monsterHeadPos - camTransform.position);
                    camTransform.rotation = Quaternion.Slerp(camTransform.rotation, targetRot, t * 5f);
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);

            // Game Over
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
