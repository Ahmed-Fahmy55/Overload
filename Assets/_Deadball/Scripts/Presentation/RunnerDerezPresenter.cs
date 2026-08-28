using System.Collections.Generic;
using Core.Events;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Derezzes a knocked-out runner (OVERLOAD GDD 17, 20 MMF_KO, 22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Suit integrity fails - the runner derezzes" (6.5). Until this existed a KO'd runner simply
    /// stopped responding and stood there, which read as a frozen bug rather than as a death.
    /// </para>
    /// <para>
    /// The dissolve material is built at runtime from whatever the runner is actually wearing, so it
    /// works for every Synty character in the roster without a hand-authored material per model. The
    /// originals are kept and restored between rounds rather than reloaded.
    /// </para>
    /// </remarks>
    public class RunnerDerezPresenter : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] Fighter _fighter;

        [Tooltip("Overload/RunnerDerez. A URP dissolve - the shader packs only ship built-in ones.")]
        [Required, SerializeField] Shader _derezShader;

        [Tooltip("Any tiling greyscale noise. Hovl's Noise* textures work well.")]
        [Required, SerializeField] Texture2D _noise;

        [Title("Timing")]
        [Tooltip("Unscaled, so the KO slow-mo does not stretch the derez into a crawl.")]
        [SuffixLabel("s", true), MinValue(0.05f), SerializeField] float _duration = 0.75f;

        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _delay = 0.12f;

        [Title("Look")]
        [ColorUsage(true, true), SerializeField]
        Color _edgeColour = new(0.6f, 3f, 5f, 1f);

        [MinValue(0.001f), SerializeField] float _edgeWidth = 0.1f;
        [MinValue(0.01f), SerializeField] float _noiseScale = 5f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsDerezzing { get; private set; }

        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int NoiseMapId = Shader.PropertyToID("_NoiseMap");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");

        readonly List<Renderer> _renderers = new();
        readonly List<Material[]> _originals = new();
        readonly List<Material> _spawned = new();

        EventBinding<FighterKnockedOut> _knockedOut;
        EventBinding<RoundStarting> _roundStarting;
        float _elapsed = -1f;

        void OnEnable()
        {
            _knockedOut = new EventBinding<FighterKnockedOut>(OnKnockedOut);
            _roundStarting = new EventBinding<RoundStarting>(Restore);

            EventBus<FighterKnockedOut>.Register(_knockedOut);
            EventBus<RoundStarting>.Register(_roundStarting);
        }

        void OnDisable()
        {
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
            EventBus<RoundStarting>.Deregister(_roundStarting);
            Restore();
        }

        void Update()
        {
            if (!IsDerezzing) return;

            _elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01((_elapsed - _delay) / _duration);
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) _spawned[i].SetFloat(DissolveId, t);

            if (t < 1f) return;

            // Fully gone: the renderers come off so nothing lingers as an invisible silhouette in
            // the depth buffer or catches a light.
            foreach (Renderer r in _renderers)
                if (r != null) r.enabled = false;

            IsDerezzing = false;
        }

        void OnKnockedOut(FighterKnockedOut evt)
        {
            if (_fighter == null || evt.Slot != _fighter.Slot || IsDerezzing) return;
            if (_derezShader == null) return;

            Collect();

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                Material[] source = _originals[i];
                var swapped = new Material[source.Length];

                for (int m = 0; m < source.Length; m++)
                {
                    var derez = new Material(_derezShader);

                    // Carried across so the runner keeps its own texture and tint while it burns.
                    if (source[m] != null)
                    {
                        if (source[m].HasProperty(BaseMapId))
                            derez.SetTexture(BaseMapId, source[m].GetTexture(BaseMapId));
                        if (source[m].HasProperty(BaseColorId))
                            derez.SetColor(BaseColorId, source[m].GetColor(BaseColorId));
                    }

                    derez.SetTexture(NoiseMapId, _noise);
                    derez.SetColor(EdgeColorId, _edgeColour);
                    derez.SetFloat(EdgeWidthId, _edgeWidth);
                    derez.SetFloat(NoiseScaleId, _noiseScale);
                    derez.SetFloat(DissolveId, 0f);

                    swapped[m] = derez;
                    _spawned.Add(derez);
                }

                renderer.materials = swapped;
            }

            _elapsed = 0f;
            IsDerezzing = true;
        }

        void Collect()
        {
            _renderers.Clear();
            _originals.Clear();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                // Only the body. The charge ring, status bar and any particle systems are not part
                // of the runner and should not dissolve with it.
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
                if (renderer is LineRenderer) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;

                _renderers.Add(renderer);
                _originals.Add(renderer.sharedMaterials);
            }
        }

        void Restore()
        {
            IsDerezzing = false;
            _elapsed = -1f;

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].materials = _originals[i];
                _renderers[i].enabled = true;
            }

            foreach (Material m in _spawned)
                if (m != null) Destroy(m);

            _spawned.Clear();
            _renderers.Clear();
            _originals.Clear();
        }
    }
}
