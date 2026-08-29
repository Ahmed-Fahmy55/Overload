using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Keeps the mouse pointer out of the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in this game is aimed with a mouse - section 12 is explicit that four inputs and no
    /// pointer is what lets two people share one machine - so a cursor sitting over the deck is
    /// only ever a distraction.
    /// </para>
    /// <para>
    /// Re-applied whenever the application regains focus, because Unity releases the lock on focus
    /// loss and on Escape. Without that the pointer reappears the first time a player alt-tabs and
    /// never goes away again.
    /// </para>
    /// </remarks>
    public static class CursorLock
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Apply();

            var go = new GameObject("[CursorLock]");
            go.AddComponent<CursorLockKeeper>();
            Object.DontDestroyOnLoad(go);
        }

        internal static void Apply()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>Re-applies the lock after focus changes.</summary>
    public class CursorLockKeeper : MonoBehaviour
    {
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) CursorLock.Apply();
        }
    }
}
