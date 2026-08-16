using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HMIController : MonoBehaviour
{
    [SerializeField] private Material displayMaterial;
    [SerializeField] private Texture[] displayTexture;
    [SerializeField] private KeyCode up = KeyCode.UpArrow;
    [SerializeField] private KeyCode down = KeyCode.DownArrow;

    private const string egoTag = "Ego"; // Tag for the ego vehicle
    private int currentTextureIndex = 0;
    
    public Texture[] DisplayTexture
    {
        get { return displayTexture; }

        set{ displayTexture = value; }
    }

    // 0 black
    // 1 bike
    // 2 stop

    private Renderer rend;
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // Start is called before the first frame update
    void Start()
    {
        currentTextureIndex = 0;
        rend = GetComponent<Renderer>();
        if (displayMaterial != null)
        {
            rend.material = displayMaterial;
        }

        ApplyTexture(currentTextureIndex);
    }
    
    private void SetMaterialIndex(int textureIndex)
    {
        Debug.Log("Setting material index to: " + textureIndex);
        ApplyTexture(textureIndex);
    }
    /*
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(egoTag))
        {
            // Debug.Log("HMI Triggered");
            // Debug.Log("Current Texture Index: " + currentTextureIndex);
            // Debug.Log("Total Textures: " + displayTexture.Length);
            if (currentTextureIndex < (displayTexture.Length - 1))
            {
                currentTextureIndex += 1;
                SetMaterialIndex(currentTextureIndex);

            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(egoTag))
        {
            Debug.Log("HMI Trigger Exited");
            if (currentTextureIndex > 0)
            {
                currentTextureIndex -= 1;
                SetMaterialIndex(currentTextureIndex);
            }
        }

    }
    */
    public void SetTexture(int index)
    {
        currentTextureIndex = index;  // Track current state
        ApplyTexture(index);
    }
    
    public int CurrentHMIState
    {
        get { return currentTextureIndex; }
    }

    private void ApplyTexture(int textureIndex)
    {
        if (rend == null || displayTexture == null || displayTexture.Length == 0)
        {
            return;
        }

        if (textureIndex < 0 || textureIndex >= displayTexture.Length)
        {
            return;
        }
        
        currentTextureIndex = textureIndex;  // Ensure state is tracked

        Texture tex = displayTexture[textureIndex];
        Material mat = rend.material;

        mat.mainTexture = tex;
        mat.SetTexture(BaseMapId, tex);
        mat.SetColor(BaseColorId, Color.white);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
    }
}
