using UnityEngine;
using System.Collections;

public class TrafficLightController : MonoBehaviour
{
    // Enumerate the three state of the traffic light
    public enum LightState { Off, Red, Green }

    [Header("Bulb Renderer")]
    [SerializeField] private Renderer Renderer;      // Drag Bulb here
 

    [Header("Emission Intensity (HDR)")]
    [SerializeField] private float emissiveIntensity = 50f;

    // private save the instantiated materials
    private Material MatInstance;
    private Color baseEmissionColor;
    private static readonly int ID_EmissionColor = Shader.PropertyToID("_EmissionColor");


    private void Awake()
    {
        // .material automatically copy a separate material, so don't call it frequently in Update
        MatInstance = Renderer.material;
        if (MatInstance.HasProperty(ID_EmissionColor))
            baseEmissionColor = MatInstance.GetColor(ID_EmissionColor);
        else
            Debug.LogWarning("Material has no _EmissionColor. Are you using URP/Lit or Standard?");
        Renderer.enabled = false;
        MatInstance.SetColor(ID_EmissionColor, Color.black);
        MatInstance.DisableKeyword("_EMISSION");
    }

    // External API: Switch state
    public void SetState(bool isBlinking)
    {
        
        if (isBlinking)
        {
            // Blink
            Renderer.enabled = true;
            SetEmission(MatInstance, baseEmissionColor, emissiveIntensity);
            // Debug.Log("color is " + baseEmissionColor + ", intensity is " + emissiveIntensity);
        }
        else
        {
            // Steady
            Renderer.enabled = false;
            MatInstance.SetColor(ID_EmissionColor, Color.black);
            MatInstance.DisableKeyword("_EMISSION");
        }
    }


    // Private utility: Turn on / off self-illumination
    private void SetEmission(Material mat, Color emissiveColor, float intensity)
    {
        // URP/Lit uses _EmissionColor
        mat.SetColor(ID_EmissionColor, emissiveColor * intensity);
        mat.EnableKeyword("_EMISSION");
    }

    
}
