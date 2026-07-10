using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
    public static class CanvasOptimizer
    {
        #region Sub-Canvas Isolation

        public static void IsolateInSubCanvas(Component target, bool addRaycaster = false)
        {
            if (target == null) return;
            var go = target.gameObject;
            if (go.GetComponent<Canvas>() != null) return;

            var sub = go.AddComponent<Canvas>();
            sub.overrideSorting = false;

            if (addRaycaster && go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();
        }

        public static void IsolateAll(IList<TMP_Text> texts, bool addRaycaster = false)
        {
            if (texts == null) return;
            for (int i = 0; i < texts.Count; i++)
                IsolateInSubCanvas(texts[i], addRaycaster);
        }

        public static void IsolateAll(IList<Graphic> graphics, bool addRaycaster = false)
        {
            if (graphics == null) return;
            for (int i = 0; i < graphics.Count; i++)
                IsolateInSubCanvas(graphics[i], addRaycaster);
        }

        #endregion

    }
}
