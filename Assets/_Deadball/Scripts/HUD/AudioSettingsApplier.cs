using Ami.BroAudio;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Pushes the saved volumes into BroAudio.
    /// </summary>
    /// <remarks>
    /// The settings asset is the single place the choice lives, so it survives a scene change and
    /// the trip into a match. This applies it wherever it is dropped and whenever a slider moves.
    /// </remarks>
    public class AudioSettingsApplier : MonoBehaviour
    {
        [Required, SerializeField] MatchSettings _settings;

        void OnEnable() => Apply();

        /// <summary>Re-applies every level. Called by the sliders as they move.</summary>
        public void Apply()
        {
            if (_settings == null) return;

            // Master multiplies the others rather than being a separate bus, so pulling it down
            // takes music and effects with it the way a player expects.
            float master = _settings.MasterVolume;

            BroAudio.SetVolume(BroAudioType.Music, master * _settings.MusicVolume);
            BroAudio.SetVolume(BroAudioType.SFX, master * _settings.SfxVolume);
            BroAudio.SetVolume(BroAudioType.UI, master * _settings.SfxVolume);
            BroAudio.SetVolume(BroAudioType.Ambience, master * _settings.SfxVolume);
        }

        public void SetMaster(float value)
        {
            _settings.MasterVolume = value;
            Apply();
        }

        public void SetMusic(float value)
        {
            _settings.MusicVolume = value;
            Apply();
        }

        public void SetSfx(float value)
        {
            _settings.SfxVolume = value;
            Apply();
        }
    }
}
