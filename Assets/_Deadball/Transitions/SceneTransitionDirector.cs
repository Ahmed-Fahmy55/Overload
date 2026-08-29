using System.Collections;
using Core.Events;
using Deadball.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deadball.Transitions
{
    /// <summary>
    /// Fades out, loads the next scene, then fades back in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fade is drawn here rather than handed to TransitionsPlus. That package's API is a single
    /// fire-and-forget call, which cannot express "cover the screen, wait for an async load that
    /// takes an unknown length of time, then reveal" - the reveal would start on a timer instead of
    /// on the load finishing, and a slow load would show a half-built scene.
    /// </para>
    /// <para>
    /// It installs itself instead of living in a scene. A missing director would mean menu buttons
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

        const float FadeOutSeconds = 0.35f;
        const float FadeInSeconds = 0.5f;
        const float MinimumCoveredSeconds = 0.25f;

        EventBinding<SceneLoadRequested> _requested;
        CanvasGroup _group;
        bool _busy;

        void OnEnable()
        {
            BuildCurtain();

            _requested = new EventBinding<SceneLoadRequested>(OnRequested);
            EventBus<SceneLoadRequested>.Register(_requested);
        }

        void OnDisable() => EventBus<SceneLoadRequested>.Deregister(_requested);

        void BuildCurtain()
        {
            var canvasGo = new GameObject("Curtain");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above everything, including a match-end card that may already be on screen.
            canvas.sortingOrder = short.MaxValue;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var imageGo = new GameObject("Fill", typeof(RectTransform));
            imageGo.transform.SetParent(canvasGo.transform, false);
            imageGo.layer = canvasGo.layer;

            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;
        }

        void OnRequested(SceneLoadRequested evt)
        {
            if (_busy || string.IsNullOrEmpty(evt.SceneName)) return;

            _busy = true;
            StartCoroutine(Run(evt.SceneName));
        }

        IEnumerator Run(string sceneName)
        {
            // A KO's slow-mo can still be running when someone picks BACK TO MENU, and every fade
            // here is on unscaled time so a stopped clock cannot strand the curtain half drawn.
            Time.timeScale = 1f;

            _group.blocksRaycasts = true;
            yield return Fade(0f, 1f, FadeOutSeconds);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            load.allowSceneActivation = false;

            // Unity stalls a non-activating load at 0.9, which is its way of saying "ready".
            while (load.progress < 0.9f) yield return null;

            // A load that finishes instantly would otherwise flash the curtain, which reads as a
            // glitch rather than as a transition.
            yield return new WaitForSecondsRealtime(MinimumCoveredSeconds);

            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;

            // One frame for the new scene to wake and draw before the curtain lifts on it.
            yield return null;

            yield return Fade(1f, 0f, FadeInSeconds);
            _group.blocksRaycasts = false;

            _busy = false;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            _group.alpha = to;
        }
    }
}
