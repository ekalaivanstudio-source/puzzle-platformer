using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Coordinator for dynamic route network energy propagation in Level Selection.
    /// Drives sequential traveling pulses along dynamically generated path segments,
    /// triggering node ignition and bubble bursts as the current advances.
    /// </summary>
    public class UIRouteEnergy : MonoBehaviour
    {
        [Header("Route Energy Configuration")]
        [SerializeField] private Color m_HeadColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private Color m_TailColor = new Color(1.0f, 0.82f, 0.22f, 1.0f);
        [SerializeField] private float m_SegmentDuration = 0.14f;

        private Coroutine m_ActivePropagation;

        public void PropagateEnergyThroughSegments(IList<UIPathRouteEffect> segments, Action<int> onNodeReached = null, Action onComplete = null)
        {
            if (m_ActivePropagation != null)
            {
                StopCoroutine(m_ActivePropagation);
            }

            if (segments == null || segments.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            m_ActivePropagation = StartCoroutine(PropagationRoutine(segments, onNodeReached, onComplete));
        }

        private IEnumerator PropagationRoutine(IList<UIPathRouteEffect> segments, Action<int> onNodeReached, Action onComplete)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                UIPathRouteEffect seg = segments[i];
                if (seg == null) continue;

                int nodeIndex = i + 1;
                bool segDone = false;

                seg.PlayTravelPulse(m_SegmentDuration, () =>
                {
                    onNodeReached?.Invoke(nodeIndex);
                }, () =>
                {
                    segDone = true;
                });

                while (!segDone)
                {
                    yield return null;
                }
            }

            m_ActivePropagation = null;
            onComplete?.Invoke();
        }

        public void StopPropagation()
        {
            if (m_ActivePropagation != null)
            {
                StopCoroutine(m_ActivePropagation);
                m_ActivePropagation = null;
            }
        }
    }
}
