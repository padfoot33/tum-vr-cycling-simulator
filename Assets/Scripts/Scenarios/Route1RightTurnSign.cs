using UnityEngine;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// World-space StVO 214-10 (mandatory right) at the Route 1 turn.
    /// </summary>
    public static class Route1RightTurnSign
    {
        public const string ObjectName = "Route1_RightTurn_Sign";
        public const string TexturePath = "Assets/3d_model/Textures/Sign_214-10.png";

        public static Transform Ensure(Transform rightTurnTrigger, Texture2D texture = null)
        {
            if (rightTurnTrigger == null)
                return null;

            Transform existing = rightTurnTrigger.parent != null
                ? rightTurnTrigger.parent.Find(ObjectName)
                : null;
            if (existing == null)
            {
                var named = GameObject.Find(ObjectName);
                if (named != null) existing = named.transform;
            }

            if (existing != null)
            {
                Place(existing, rightTurnTrigger);
                return existing;
            }

            var root = new GameObject(ObjectName);
            if (rightTurnTrigger.parent != null)
                root.transform.SetParent(rightTurnTrigger.parent, true);
            Place(root.transform, rightTurnTrigger);
            BuildVisual(root.transform, texture);
            return root.transform;
        }

        public static void Place(Transform sign, Transform rightTurnTrigger)
        {
            Vector3 triggerPos = rightTurnTrigger.position;
            Vector3 incoming = new Vector3(0.919f, 0f, -0.394f);
            Vector3 right = Vector3.Cross(Vector3.up, incoming).normalized;
            Vector3 pos = triggerPos - incoming * 6f + right * 3.6f;
            pos.y = triggerPos.y;
            sign.position = pos;
            sign.rotation = Quaternion.LookRotation(-incoming, Vector3.up);
        }

        private static void BuildVisual(Transform root, Texture2D texture)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root, false);
            pole.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 1.35f, 0.08f);
            StripCollider(pole);
            SetColor(pole, new Color(0.22f, 0.22f, 0.22f));

            if (texture == null)
                texture = LoadTexture();

            CreateFace(root, "Face_Front", new Vector3(0f, 2.55f, 0.03f), false, texture);
            CreateFace(root, "Face_Back", new Vector3(0f, 2.55f, -0.03f), true, texture);
        }

        private static void CreateFace(Transform root, string name, Vector3 localPos, bool flip, Texture2D texture)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, flip ? 180f : 0f, 0f);
            quad.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            StripCollider(quad);

            var renderer = quad.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (texture != null)
                mat.mainTexture = texture;
            else
                mat.color = new Color(0.15f, 0.45f, 0.85f);
            renderer.sharedMaterial = mat;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }

        private static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }

        private static Texture2D LoadTexture()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
#else
            return null;
#endif
        }
    }
}
