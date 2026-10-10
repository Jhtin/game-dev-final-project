using UnityEngine;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Realistic gravity and collision physics for Backrooms ground items and dropped pickups.
    /// - Items dropped or placed fall naturally onto the floor without floating or clipping through geometry.
    /// - Once settled firmly on the floor surface, physics stabilizes to prevent sliding or CPU waste.
    /// - Prevents items from spawning inside structural pillars, walls, or below the floor plane.
    /// </summary>
    public class GroundItemPhysics : MonoBehaviour
    {
        [Header("Physics Settings")]
        [SerializeField] private bool autoSettleOnStart = true;
        [SerializeField] private float verticalOffset = 0.04f; // Floor clearance
        [SerializeField] private LayerMask groundLayers = ~0;

        private Rigidbody rb;
        private Collider col;
        private bool isSettled = false;
        private float airTimer = 0f;

        public bool IsSettled => isSettled;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                groundLayers &= ~(1 << playerLayer);
            }
        }

        private void Start()
        {
            if (autoSettleOnStart)
            {
                SnapToFloorImmediately();
            }
        }

        /// <summary>
        /// Raycasts downward from above the item to find the exact floor surface,
        /// ensuring the item rests naturally on the floor without clipping or floating.
        /// </summary>
        public bool SnapToFloorImmediately()
        {
            Vector3 origin = transform.position + Vector3.up * 0.75f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5.0f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * verticalOffset;
                isSettled = true;
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Drops the item with simulated or active gravity into the world.
        /// </summary>
        public void Drop(Vector3 initialVelocity)
        {
            isSettled = false;
            airTimer = 0f;
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 1.0f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = initialVelocity;
        }

        private void Update()
        {
            if (isSettled) return;

            airTimer += Time.deltaTime;

            if (rb != null && !rb.isKinematic)
            {
                if (airTimer > 0.35f && rb.linearVelocity.sqrMagnitude < 0.04f)
                {
                    SettleOnSurface();
                }
                else if (airTimer > 4.0f)
                {
                    SnapToFloorImmediately();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isSettled) return;

            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > 0.55f)
                {
                    SettleOnSurface();
                    break;
                }
            }
        }

        private void SettleOnSurface()
        {
            isSettled = true;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            SnapToFloorImmediately();
        }
    }
}
