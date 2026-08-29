using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// The menu's music bed.
    /// </summary>
    /// <remarks>
    /// Deliberately not part of <see cref="Deadball.Presentation.OverloadAudioDirector"/>: that one
    /// exists to turn match events into cues and has no business being alive on a screen where no
    /// match is running. This is one track, started and stopped with the screen.
    /// </remarks>
    public class MenuMusic : MonoBehaviour
    {
        [Required, SerializeField] SoundID _track;

        [Tooltip("Faded rather than cut, so leaving the menu does not click.")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _fadeOut = 0.6f;

        void OnEnable()
        {
            if (_track.IsValid()) BroAudio.Play(_track);
        }

        void OnDisable()
        {
            // The scene change destroys this object, and BroAudio would otherwise carry the menu
            // track into the match underneath the arena's own music.
            if (_track.IsValid()) BroAudio.Stop(_track, _fadeOut);
        }
    }
}
