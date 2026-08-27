using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// The magnetise volume around a loose ball (GDD section 6.3).
    /// </summary>
    /// <remarks>
    /// Pickup is a trigger rather than a proximity check in the ball's update because the interesting
    /// decisions happen after you have the ball - getting it should never be fiddly, and a trigger
    /// that is simply switched off outside the LOOSE state cannot mis-fire.
    /// </remarks>
    [RequireComponent(typeof(SphereCollider))]
    public class BallGrabTrigger : MonoBehaviour
    {
        SphereCollider _trigger;
        BallController _ball;

        public void Initialise(BallController ball)
        {
            _ball = ball;
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;

            if (ball.Config != null)
                _trigger.radius = ball.Config.PickupRadius;
        }

        public void SetActive(bool active)
        {
            if (_trigger != null)
                _trigger.enabled = active;
        }

        void OnTriggerEnter(Collider other) => TryGrab(other);

        // A fighter can be standing still on top of a ball that has just gone loose, in which case
        // no enter event ever fires. Stay covers that without needing a separate overlap query.
        void OnTriggerStay(Collider other) => TryGrab(other);

        void TryGrab(Collider other)
        {
            if (_ball == null || _ball.State != BallState.Loose) return;

            if (other.GetComponentInParent<IBallTarget>() is { } target)
                _ball.TryGrab(target);
        }
    }
}
