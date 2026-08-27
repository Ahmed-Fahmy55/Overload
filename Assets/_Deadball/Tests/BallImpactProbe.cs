using System.Collections;
using System.Text;
using Deadball.Ball;
using UnityEngine;

namespace Deadball.Tests
{
    /// <summary>
    /// Throws the ball at a target and logs what happens on contact, frame by frame.
    /// </summary>
    /// <remarks>
    /// A diagnostic, not a test. It exists to answer one question that assertions cannot: when a
    /// max-charge ball meets a prop, does it reflect, penetrate, or stop dead?
    /// </remarks>
    public class BallImpactProbe : MonoBehaviour
    {
        public static string LastReport = "(not run)";

        public static void Run(Vector3 from, Vector3 target, float charge01, int steps = 60)
        {
            var probe = new GameObject("BallImpactProbe").AddComponent<BallImpactProbe>();
            probe.StartCoroutine(probe.Sample(from, target, charge01, steps));
        }

        IEnumerator Sample(Vector3 from, Vector3 target, float charge01, int steps)
        {
            // The match director would start a fresh round mid-probe, which resets the ball to the
            // arena centre and makes every sample meaningless. Play-mode changes are discarded, so
            // tearing them out here costs nothing.
            foreach (var m in FindObjectsByType<Deadball.Match.MatchManager>(FindObjectsSortMode.None))
                Destroy(m);

            foreach (var r in FindObjectsByType<Deadball.Match.RoundManager>(FindObjectsSortMode.None))
            {
                r.Abort();
                Destroy(r);
            }

            yield return null;

            var ball = FindFirstObjectByType<BallController>();
            var fighter = FindFirstObjectByType<Deadball.Fighters.Fighter>();

            fighter.Motor.Teleport(from, Quaternion.identity);
            ball.ResetForRound(from);

            for (int i = 0; i < 30 && ball.State != BallState.Held; i++)
                yield return new WaitForFixedUpdate();

            if (ball.State != BallState.Held)
            {
                LastReport = "probe aborted: fighter never picked the ball up";
                Debug.LogError("[Probe] " + LastReport);
                Destroy(gameObject);
                yield break;
            }

            var rb = ball.GetComponent<Rigidbody>();
            var log = new StringBuilder();

            Vector3 start = ball.transform.position;
            Vector3 direction = (target - start);
            direction.y = 0f;
            direction.Normalize();

            var col = ball.GetComponent<SphereCollider>();
            var mat = col != null ? col.sharedMaterial : null;
            log.AppendLine($"start={start:F2} dir={direction:F2} ccd={rb.collisionDetectionMode} "
                + $"kinematic={rb.isKinematic} colEnabled={(col != null && col.enabled)} "
                + $"mat={(mat != null ? mat.name : "none")} "
                + $"bounciness={(mat != null ? mat.bounciness : -1f)} "
                + $"bounceCombine={(mat != null ? mat.bounceCombine.ToString() : "-")} "
                + $"bounceThreshold={Physics.bounceThreshold} fixedDt={Time.fixedDeltaTime}");

            ball.Throw(direction, charge01);

            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();

                log.AppendLine($"{i:00} state={ball.State} pos={ball.transform.position:F2} "
                    + $"vel={rb.linearVelocity:F2} speed={rb.linearVelocity.magnitude:F2}");

                if (ball.State != BallState.Flying) break;
            }

            LastReport = log.ToString();
            Debug.Log("[Probe]\n" + LastReport);
            Destroy(gameObject);
        }
    }
}
