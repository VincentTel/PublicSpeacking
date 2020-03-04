using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiDisplay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoadRuntimeMethod()
    {
        Screen.fullScreen = false;
        Screen.fullScreenMode = FullScreenMode.Windowed;
        //Screen.SetResolution(1920, 1080, false);

        Debug.Log("displays connected: " + Display.displays.Length);
        // Display.displays[0] is the primary, default display and is always ON, so start at index 1.
        // Check if additional displays are available and activate each.
        

        if (Display.displays.Length >= 2)
        {
            Screen.fullScreen = false;
            // Display.displays[1].SetParams(width, height, xpos, ypos);
            Display.displays[1].Activate();  
        }
            
        
    }
}
