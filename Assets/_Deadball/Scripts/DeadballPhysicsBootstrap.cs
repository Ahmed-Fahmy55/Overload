using UnityEngine;

namespace Deadball
{
    /// <summary>
    /// Applies the layer collision rules in code rather than in the project's collision matrix.
    /// </summary>
    /// <remarks>
    /// The matrix lives in ProjectSettings, which this project shares with TopDownEngine and a
    /// handful of asset-store packages. Setting the three rules that matter at load keeps them next
    /// to the layer names they belong to, survives someone else touching the matrix, and cannot be
    /// silently lost in a merge.
    /// </remarks>
    public static class DeadballPhysicsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            foreach ((string a, string b) in DeadballLayers.IgnoredPairs)
            {
                int layerA = LayerMask.NameToLayer(a);
                int layerB = LayerMask.NameToLayer(b);

                if (layerA < 0 || layerB < 0)
                {
                    Debug.LogWarning($"[Deadball] Layer '{(layerA < 0 ? a : b)}' is missing. Run Deadball > Setup first.");
                    continue;
                }

                Physics.IgnoreLayerCollision(layerA, layerB, true);
            }
        }
    }
}
