using Core.Events;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The four sounds that carry the game (GDD section 18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The full audio pass is Day 2, but two of these are Day 1 work because they are part of the
    /// catch rather than part of the polish: the flash ping is the cue the player presses on, and the
    /// thunk is the reward that tells them they read it right. Slots left empty simply do not play,
    /// so the component can be wired now and filled in later.
    /// </para>
    /// <para>
    /// The thunk is mixed above everything else on purpose. It is the itch.io GIF and the reason
    /// anyone remembers the game.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public class MatchAudioCues : MonoBehaviour
    {
        [Title("Clips")]
        [Tooltip("Pitch scales with charge.")]
        [SerializeField] AudioClip _throwWhoosh;

        [Tooltip("The catch cue. Short, rising, unmistakable, mixed above the music.")]
        [SerializeField] AudioClip _flashPing;

        [Tooltip("The loudest sound in the build.")]
        [SerializeField] AudioClip _catchThunk;

        [SerializeField] AudioClip _knockImpact;

        [Title("Mix")]
        [PropertyRange(0f, 2f), SerializeField] float _throwVolume = 0.7f;
        [PropertyRange(0f, 2f), SerializeField] float _pingVolume = 1f;
        [PropertyRange(0f, 2f), SerializeField] float _thunkVolume = 1.4f;
        [PropertyRange(0f, 2f), SerializeField] float _knockVolume = 1.1f;

        [Title("Throw Pitch")]
        [SerializeField] float _minThrowPitch = 0.85f;
        [SerializeField] float _maxThrowPitch = 1.35f;

        AudioSource _source;
        EventBinding<BallThrown> _thrown;
        EventBinding<BallFlashCue> _flash;
        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;

        void Awake() => _source = GetComponent<AudioSource>();

        void OnEnable()
        {
            _thrown = new EventBinding<BallThrown>(OnThrown);
            _flash = new EventBinding<BallFlashCue>(() => Play(_flashPing, _pingVolume));
            _caught = new EventBinding<BallCaught>(() => Play(_catchThunk, _thunkVolume));
            _knocked = new EventBinding<FighterKnocked>(() => Play(_knockImpact, _knockVolume));
            _knockedOut = new EventBinding<FighterKnockedOut>(() => Play(_knockImpact, _knockVolume));

            EventBus<BallThrown>.Register(_thrown);
            EventBus<BallFlashCue>.Register(_flash);
            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
        }

        void OnDisable()
        {
            EventBus<BallThrown>.Deregister(_thrown);
            EventBus<BallFlashCue>.Deregister(_flash);
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
        }

        void OnThrown(BallThrown evt)
        {
            if (_throwWhoosh == null) return;

            _source.pitch = Mathf.Lerp(_minThrowPitch, _maxThrowPitch, evt.Charge01);
            _source.PlayOneShot(_throwWhoosh, _throwVolume);
            _source.pitch = 1f;
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip != null)
                _source.PlayOneShot(clip, volume);
        }
    }
}
