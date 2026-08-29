using System.Collections;
using Core.Events;
using Deadball.Events;
using TransitionsPlus;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deadball.Transitions
{
    /// <summary>
    /// Covers the screen with a TransitionsPlus effect, loads the next scene, then reveals it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effect is the package's Dissolve, which is the same visual language as the runner derez
    /// on a KO, so leaving a deck reads as the deck powering down rather than as a plain fade.
    /// </para>
    /// <para>
    /// The package's own <c>sceneNameToLoad</c> is not used. It loads on the effect ending, which
    /// means the reveal is timed off the effect rather than off the load, and a slow load would show
    /// a half-built scene. Driving it here keeps the screen covered until the load actually reports
    /// ready.
    /// </para>
    /// <para>
    /// Nothing in TransitionsPlus survives a Single-mode load - the animator is an ordinary scene
    /// object - so the cover is explicitly marked <see cref="Object.DontDestroyOnLoad"/>, and the
    /// reveal is started a frame before the cover is torn down so no frame is left uncovered.
    /// </para>
    /// <para>
    /// It installs itself rather than living in a scene. A missing director would mean menu buttons
    /// that quietly do nothing, and that is exactly the failure nobody notices until the build.
    /// </para>
    /// </remarks>
    public class SceneTransitionDirector : MonoBehaviour
    {
        static SceneTransitionDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("[SceneTransitionDirector]");
            _instance = go.AddComponent<SceneTransitionDirector>();
            DontDestroyOnLoad(go);
        }

        const TransitionType Effect = TransitionType.Dissolve;
        const float CoverSeconds = 0.45f;
        const float RevealSeconds = 0.6f;
        const float MinimumCoveredSeconds = 0.25f;

        // Above every canvas in the game, including the pause menu at 500 and a match-end card.
        const int Sorting = 30000;

        static readonly Color Curtain = new(0.02f, 0.03f, 0.05f);

        EventBinding<SceneLoadRequested> _requested;
        bool _busy;

        void OnEnable()
        {
            _requested = new EventBinding<SceneLoadRequested>(OnRequested);
            EventBus<SceneLoadRequested>.Register(_requested);
        }

        void OnDisable() => EventBus<SceneLoadRequested>.Deregister(_requested);

        void OnRequested(SceneLoadRequested evt)
        {
            if (_busy || string.IsNullOrEmpty(evt.SceneName)) return;

            _busy = true;
            StartCoroutine(Run(evt.SceneName));
        }

        IEnumerator Run(string sceneName)
        {
            // A KO's slow-mo can still be running when someone picks BACK TO MENU. The transition
            // runs on unscaled time, but the load itself is gentler with the clock back to normal.
            Time.timeScale = 1f;

            TransitionAnimator cover = Play(CoverSeconds, invert: false);
            KeepAcrossLoad(cover);

            yield return WaitFor(cover, CoverSeconds);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            load.allowSceneActivation = false;

            // Unity stalls a non-activating load at 0.9, which is its way of saying "ready".
            while (load.progress < 0.9f) yield return null;

            // A load that finishes instantly would otherwise flash the effect, which reads as a
            // glitch rather than as a transition.
            yield return new WaitForSecondsRealtime(MinimumCoveredSeconds);

            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;

            // One frame for the new scene to wake and draw underneath the cover.
            yield return null;

            // Started before the cover is destroyed: an inverted transition begins fully covered,
            // so for one frame both are drawn and nothing shows through the seam.
            TransitionAnimator reveal = Play(RevealSeconds, invert: true);
            KeepAcrossLoad(reveal);
            yield return null;

            if (cover != null) Destroy(cover.transform.root.gameObject);

            yield return WaitFor(reveal, RevealSeconds);

            if (reveal != null) Destroy(reveal.transform.root.gameObject);

            _busy = false;
        }

        static TransitionAnimator Play(float seconds, bool invert)
        {
            TransitionAnimator animator = TransitionAnimator.Start(
                Effect,
                duration: seconds,
                color: Curtain,
                invert: invert,
                autoDestroy: false,
                sortingOrder: Sorting);

            // A KO's slow-mo can still be winding down as the curtain starts, and a transition on
            // scaled time would crawl through it.
            if (animator != null) animator.useUnscaledTime = true;

            return animator;
        }

        /// <summary>Waits for a transition to run to its end.</summary>
        /// <remarks>
        /// Two loops, not one. The package begins playback from a delayed invoke inside its own
        /// Start, so <c>isPlaying</c> is still false on the frame the transition is created -
        /// waiting on it directly falls straight through and the screen is never actually covered.
        /// The deadline is the backstop for a transition that never reports playing at all, so a
        /// failure here degrades to a hard cut rather than a hang on a black screen.
        /// </remarks>
        static IEnumerator WaitFor(TransitionAnimator animator, float seconds)
        {
            if (animator == null) yield break;

            float deadline = Time.realtimeSinceStartup + seconds + 0.5f;

            while (animator != null && !animator.isPlaying
                   && Time.realtimeSinceStartup < deadline) yield return null;

            while (animator != null && animator.isPlaying
                   && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Keeps a transition alive through the Single-mode load it is covering.</summary>
        static void KeepAcrossLoad(TransitionAnimator animator)
        {
            if (animator == null) return;

            DontDestroyOnLoad(animator.transform.root.gameObject);
        }
    }
}
