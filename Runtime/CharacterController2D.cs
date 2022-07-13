using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FourthSun
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class CharacterController2D : MonoBehaviour
    {
        private const float EPSILON = .0001f;

        [Serializable]
        private struct CalculationCache
        {
            public Bounds bounds;
            public Vector2 position;
            public Vector2 directions;
            public Ray2D[] posXRays;
            public Ray2D[] negXRays;
            public Ray2D[] posYRays;
            public Ray2D[] negYRays;
        }

        [SerializeField]
        [Tooltip("The character's collision skin width.")]
        [Min(0.0001f)]
        private float skinWidth = .08f;

        [SerializeField]
        [Tooltip("The minimum move distance of the character controller.")]
        [Min(0)]
        private float minMoveDistance;

        [SerializeField]
        [Tooltip("The number of rays to calculate collisions with.\nA value of 2 will only detect collisions at the corners, while a value too high will impact performance.")]
        [Min(2)]
        private int rayCount = 5;

        [Tooltip("The layer mask to use when detecting collisions.")]
        // default to "Nothing"
        public LayerMask layerMask = 0;

        /// <summary>
        /// Contains what part of the controller collided with the environment during the last CharacterController2D.Move call. 
        /// </summary>
        public CollisionFlags2D CollisionFlags { get; private set; } = CollisionFlags2D.None;
        /// <summary>
        /// Contains what part of the controller collided with the environment before the last CharacterController2D.Move call.
        /// </summary>
        public CollisionFlags2D PrevCollisionFlags { get; private set; } = CollisionFlags2D.None;
        /// <summary>
        /// Contains whether the controller was touching the ground during the last CharacterController2D.Move call. 
        /// </summary>
        public bool IsGrounded => CollisionFlags.HasFlag(CollisionFlags2D.Below);
        /// <summary>
        /// Contains whether the controller was touching the ground before the last CharacterController2D.Move call.
        /// </summary>
        public bool WasGrounded => PrevCollisionFlags.HasFlag(CollisionFlags2D.Below);
        /// <summary>
        /// The current relative velocity of the controller.
        /// </summary>
        public Vector2 Velocity { get; private set; }

        private CalculationCache calculationCache;

        private new BoxCollider2D collider;

        private void OnValidate()
        {
            layerMask &= ~(1 << gameObject.layer);
        }

        private void Awake()
        {
            collider = GetComponent<BoxCollider2D>();
            calculationCache.posXRays = new Ray2D[rayCount];
            calculationCache.negXRays = new Ray2D[rayCount];
            calculationCache.posYRays = new Ray2D[rayCount];
            calculationCache.negYRays = new Ray2D[rayCount];
        }

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        /// <summary>
        /// Sets up a cache of values for the controller to use in CharacterController2D.Move calls
        /// to increase performance.
        /// You need to call this if you change the size or bounds of the BoxCollider2D attached
        /// to the controller at runtime!
        /// </summary>
        public void Refresh()
        {
            if (calculationCache.bounds.size == collider.bounds.size) return;
            var bounds = new Bounds(Vector2.zero, collider.bounds.size);
            calculationCache.bounds = bounds;

            var xRayDistance = bounds.size.y / (rayCount - 1);
            var yRayDistance = bounds.size.x / (rayCount - 1);
            for (var i = 0; i < rayCount; i++)
            {
                var xRayStep = i * xRayDistance;
                var yRayStep = i * yRayDistance;
                var yOffset = i == 0 || i == rayCount - 1 ? Mathf.Sign(i - 1) * EPSILON : 0;

                var posXOrigin = new Vector2(bounds.max.x - skinWidth, bounds.max.y - xRayStep);
                calculationCache.posXRays[i] = new Ray2D(posXOrigin, Vector2.right);
 
                var negXOrigin = new Vector2(bounds.min.x + skinWidth, bounds.max.y - xRayStep);
                calculationCache.negXRays[i] = new Ray2D(negXOrigin, Vector2.left);

                var posYOrigin = new Vector2(bounds.min.x + yRayStep - yOffset, bounds.max.y - skinWidth);
                calculationCache.posYRays[i] = new Ray2D(posYOrigin, Vector2.up);

                var negYOrigin = new Vector2(bounds.min.x + yRayStep - yOffset, bounds.min.y + skinWidth);
                calculationCache.negYRays[i] = new Ray2D(negYOrigin, Vector2.down);
            }
        }

        /// <summary>
        /// Supplies the movement of a GameObject with an attached CharacterController2D component.
        /// </summary>
        /// <param name="motion"></param>
        public void Move(Vector2 motion)
        {
            PrevCollisionFlags = CollisionFlags; 
            calculationCache.position = transform.position;
            Velocity = CalculateVelocity();
            if (motion.magnitude < minMoveDistance) return;

            CollisionFlags = CollisionFlags2D.None;
            calculationCache.directions.x = motion.x == 0 ? calculationCache.directions.x : Mathf.Sign(motion.x);
            calculationCache.directions.y = motion.y == 0 ? calculationCache.directions.y : Mathf.Sign(motion.y);
            var directions = calculationCache.directions;

            var xRays = directions.x > 0 ? calculationCache.posXRays : calculationCache.negXRays;
            var yRays = directions.y > 0 ? calculationCache.posYRays : calculationCache.negYRays;
            var xHit = EvaluateRays(xRays, Mathf.Abs(motion.x) + skinWidth);
            var yHit = EvaluateRays(yRays, Mathf.Abs(motion.y) + skinWidth);
            RegisterCollisionFlag(xHit, CollisionFlags2D.Sides);
            RegisterCollisionFlag(yHit, motion.y > 0 ? CollisionFlags2D.Above : CollisionFlags2D.Below);

            var xMovement = CalculateMovement(xHit, motion.x);
            var yMovement = CalculateMovement(yHit, motion.y);
            transform.Translate(new Vector2(xMovement, yMovement));
            Velocity = CalculateVelocity();
        }

        /// <summary>
        /// Moves the character with speed.
        /// </summary>
        /// <param name="speed"></param>
        public void SimpleMove(Vector2 speed)
        {
            var ySpeedOverride = Velocity.y + Physics2D.gravity.y * Time.deltaTime;
            var motion = new Vector2(speed.x, ySpeedOverride);
            Move(motion * Time.deltaTime);
        }

        private RaycastHit2D? EvaluateRays(IEnumerable<Ray2D> rays, float distance)
        {
            if (distance == 0) return null;
            return rays.Aggregate<Ray2D, RaycastHit2D?>(null, (acc, ray) =>
            {
                var hit = EvaluateRay(ray, distance);
                if (hit == null) return acc;
                if (acc == null) return hit;
                return hit?.distance < acc?.distance ? hit : acc;
            });
        }

        private RaycastHit2D? EvaluateRay(Ray2D ray, float distance)
        {
            Vector2 offset = transform.position;
            var origin = ray.origin + offset;
            Debug.DrawRay(origin, ray.direction * distance, Color.red);
            var hit = Physics2D.Raycast(origin, ray.direction, distance, layerMask);
            if (!hit.collider || hit.collider.OverlapPoint(origin)) return null;
            return hit;
        }

        private void RegisterCollisionFlag(RaycastHit2D? hit, CollisionFlags2D flag)
        {
            if (hit == null) return;
            CollisionFlags |= flag;
        }

        private float CalculateMovement(RaycastHit2D? hit, float motion)
        {
            if (hit == null) return motion;
            var hitDistance = (float) hit?.distance;
            var movement = hitDistance - skinWidth;
            return Mathf.Sign(motion) * movement;
        }

        private Vector2 CalculateVelocity()
        {
            Vector2 position = transform.position; 
            return (position - calculationCache.position) / Time.deltaTime;
        }
    }
}
