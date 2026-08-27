using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Catches the animation events baked into the borrowed locomotion clips.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk and run clips came from Unity's StarterAssets set and carry <c>OnFootstep</c> and
    /// <c>OnLand</c> events. Without a receiver on the animator's own GameObject, Unity logs an error
    /// on every single footfall - which buries anything real in the console.
    /// </para>
    /// <para>
    /// It is also a free hook: leave the sound unassigned and it is a silent no-op, or point it at a
    /// BroAudio entity and the deck gets footsteps.
    /// </para>
    /// </remarks>
    public class FootstepReceiver : MonoBehaviour
    {
        [Tooltip("Optional. Unassigned is a silent no-op.")]
        [SerializeField] SoundID _footstep;

        [Tooltip("Optional. Played when a locomotion clip reports a landing.")]
        [SerializeField] SoundID _land;

        [PropertyRange(0f, 1f), SerializeField] float _volume = 0.35f;

        // Signature must match the events in the clips; Unity passes the event through.
        public void OnFootstep(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight < 0.5f) return;
            Play(_footstep);
        }

        public void OnLand(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight < 0.5f) return;
            Play(_land);
        }

        void Play(SoundID id)
        {
            if (id.IsValid()) BroAudio.Play(id).SetVolume(_volume);
        }
    }
}
