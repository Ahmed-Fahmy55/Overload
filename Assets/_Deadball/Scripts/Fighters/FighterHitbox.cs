using Deadball.Ball;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>
    /// The trigger volume that turns a flying ball into a catch, a whiff, or a knock.
    /// </summary>
    /// <remarks>
    /// This is a trigger and not a collider on purpose. A physical collision would deflect the ball
    /// off a dodging fighter - which the design explicitly does not want, since a dodged ball is
    /// supposed to sail past and stay live - and it would fight the catch by applying an impulse at
    /// the exact moment possession changes hands.
    /// </remarks>
    [RequireComponent(typeof(SphereCollider))]
    public class FighterHitbox : MonoBehaviour
    {
        [Required, SerializeField] Fighter _fighter;

        void Reset()
        {
            var sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
        }

        void Awake()
        {
            var sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = _fighter.CatchRadius;
        }

        void OnTriggerEnter(Collider other) => Resolve(other);

        // A ball can go from HELD to FLYING while already overlapping this volume - a point-blank
        // throw at the fighter standing on top of you never generates an enter event.
        void OnTriggerStay(Collider other) => Resolve(other);

        void Resolve(Collider other)
        {
            if (other.GetComponentInParent<BallController>() is { } ball)
                ball.ResolveTargetContact(_fighter);
        }
    }
}
