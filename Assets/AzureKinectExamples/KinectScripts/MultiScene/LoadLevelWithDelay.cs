using UnityEngine;
using com.rfilkov.kinect;


namespace com.rfilkov.components
{
    /// <summary>
    /// This component loads the next level after the given delay in seconds.
    /// </summary>
    public class LoadLevelWithDelay : MonoBehaviour
    {
        [Tooltip("Seconds to wait before loading next level.")]
        public float waitSeconds = 0f;

        [Tooltip("Next level number. No level is loaded, if the number is negative.")]
        public int nextLevel = -1;

        [Tooltip("Whether to check for initialized KinectManager or not.")]
        public bool validateKinectManager = true;

        [Tooltip("UI-Text used to display the debug messages.")]
        public UnityEngine.UI.Text debugText;

        private float timeToLoadLevel = 0f;
        private bool levelLoaded = false;


        void Start()
        {
            timeToLoadLevel = Time.realtimeSinceStartup + waitSeconds;

            if (validateKinectManager && debugText != null)
            {
                KinectManager kinectManager = KinectManager.Instance;

                if (kinectManager == null || !kinectManager.IsInitialized())
                {
                    debugText.text = "KinectManager is not initialized!";
                    levelLoaded = true;
                }
            }
        }


        void Update()
        {
            if (!levelLoaded && nextLevel >= 0)
            {
                if (Time.realtimeSinceStartup >= timeToLoadLevel)
                {
                    levelLoaded = true;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextLevel);
                }
                else
                {
                    float timeRest = timeToLoadLevel - Time.realtimeSinceStartup;

                    if (debugText != null)
                    {
                        debugText.text = string.Format("Time to the next level: {0:F0} s.", timeRest);
                    }
                }
            }
        }

    }
}
