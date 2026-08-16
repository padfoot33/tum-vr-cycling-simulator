using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class TextureMirror : MonoBehaviour
{
    public bool flipX = true;   // 水平镜像
    public bool flipY = false;  // 垂直镜像

    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private static readonly int MainTex_ST = Shader.PropertyToID("_MainTex_ST");

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        Apply();
    }

    [ContextMenu("Apply Mirror")]
    public void Apply()
    {
        rend.GetPropertyBlock(mpb);

        // ST = (scaleX, scaleY, offsetX, offsetY)
        Vector4 st = new Vector4(1, 1, 0, 0);

        // 从材质读初值（若存在）
        if (rend.sharedMaterial && rend.sharedMaterial.HasProperty(MainTex_ST))
            st = rend.sharedMaterial.GetVector(MainTex_ST);

        if (flipX) { st.x = -Mathf.Abs(st.x); st.z = 1 - st.z; }
        if (flipY) { st.y = -Mathf.Abs(st.y); st.w = 1 - st.w; }

        mpb.SetVector(MainTex_ST, st);
        rend.SetPropertyBlock(mpb);
    }

    [ContextMenu("Reset Mirror")]
    public void ResetMirror()
    {
        rend.GetPropertyBlock(mpb);
        mpb.Clear();              // 清除覆盖，恢复材质默认
        rend.SetPropertyBlock(mpb);
    }
}
