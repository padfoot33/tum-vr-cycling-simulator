using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaveScreenStarter : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        bool fullscreen = true; // Fullscreen or windowed mode

        // Check if there are multiple displays connected and attempt to activate each.
        if (Display.displays.Length > 1)
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                
                Display.displays[i].Activate();
                        // Then set resolution and fullscreen mode
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, fullscreen);

                Debug.Log($"Display {i} activated. "); // Log each display activation for confirmation.

            }
            
        }
        else
        {
            // Provide a more descriptive error message.
         //  Debug.LogWar("Insufficient displays detected. Ensure multiple displays are connected and recognized by the system. This is OK in Play Mode, in the Unity Editor.");
        }

    }
}
