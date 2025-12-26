using UnityEngine;

namespace RGizmos
{
    /// <summary>
    /// Simple runtime gizmo drawing using GL
    /// </summary>
    public static class RealtimeGizmos
    {
        private static Material lineMaterial;

        /// <summary>
        /// Initialize line material
        /// </summary>
        private static void InitLineMaterial()
        {
            if (lineMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader)
            {
                Debug.LogError("[RealtimeGizmos] Missing shader: Hidden/Internal-Colored");
                return;
            }

            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        /// <summary>
        /// Draw a line between two points
        /// </summary>
        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            InitLineMaterial();
            if (lineMaterial == null) return;

            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(start.x, start.y, start.z);
            GL.Vertex3(end.x, end.y, end.z);
            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// Draw a wire circle
        /// </summary>
        public static void DrawWireCircle(Vector3 center, float radius, int segments, Vector3 normal, Color color)
        {
            InitLineMaterial();
            if (lineMaterial == null) return;

            // Create perpendicular vectors
            Vector3 forward = Vector3.Slerp(
                normal == Vector3.up ? Vector3.forward : Vector3.up, 
                normal, 
                0.01f
            ).normalized;
            Vector3 right = Vector3.Cross(normal, forward).normalized;
            Vector3 up = Vector3.Cross(right, normal).normalized;

            // Calculate points
            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            }

            // Draw lines
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);

            for (int i = 0; i < segments; i++)
            {
                Vector3 current = points[i];
                Vector3 next = points[(i + 1) % segments];
                
                GL.Vertex3(current.x, current.y, current.z);
                GL.Vertex3(next.x, next.y, next.z);
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// Draw a point (sphere)
        /// </summary>
        public static void DrawPoint(Vector3 position, float radius, Color color)
        {
            // Draw as 3 perpendicular circles
            DrawWireCircle(position, radius, 16, Vector3.up, color);
            DrawWireCircle(position, radius, 16, Vector3.right, color);
            DrawWireCircle(position, radius, 16, Vector3.forward, color);
        }
    }
}
