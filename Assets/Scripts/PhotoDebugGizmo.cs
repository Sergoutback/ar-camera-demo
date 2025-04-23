#if UNITY_EDITOR
    using UnityEngine;
    using System.Collections.Generic;
    using UnityEditor;

    public class PhotoDebugGizmo : MonoBehaviour
    {
        public List<Vector3> relativePositions;
        public float scale = 2000f;

        void OnDrawGizmos()
        {
            if (relativePositions == null) return;

            Gizmos.color = Color.cyan;

            for (int i = 0; i < relativePositions.Count; i++)
            {
                Vector3 worldPos = transform.position + new Vector3(
                    relativePositions[i].x * scale,
                    0,
                    relativePositions[i].z * scale
                );

                Gizmos.DrawSphere(worldPos, 0.01f * scale);
                Handles.Label(worldPos + Vector3.up * 0.02f * scale, $"#{i}");
            }
        }
    }
#endif