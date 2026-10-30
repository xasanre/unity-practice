using UnityEngine;

namespace MergePrototype
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class PortraitViewport : MonoBehaviour
    {
        void OnEnable() => Fit();
        void LateUpdate() => Fit();

        void Fit()
        {
            if (Screen.height <= 0) return;
            float relative = (Screen.width / (float)Screen.height) / (9f / 16f);
            GetComponent<Camera>().rect = relative >= 1f
                ? new Rect((1f - 1f / relative) * .5f, 0, 1f / relative, 1)
                : new Rect(0, (1f - relative) * .5f, 1, relative);
        }
    }
}
