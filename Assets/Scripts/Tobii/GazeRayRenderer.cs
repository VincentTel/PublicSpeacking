using SDD.Events;
using Tobii.XR;
using UnityEngine;
using System.Collections;
using System;

public class GazeRayRenderer : MonoBehaviour
{
    public Material BoneMaterial;
    private LineRenderer _lineRenderer;

    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;

    public GameObject eyes;
    public float maxRange = 0.005f;
    public float maxEyeAngle = 60;

    Vector3 rayOrigin;
    Vector3 rayDirection;

    GazeRayRecorder saverGaze;
    protected static GazeRayRenderer instance = null;

    public static GazeRayRenderer Instance
    {
        get
        {
            return instance;
        }
    }

    // recording parameters
    private bool isRecording = false;
    private bool isPlaying = false;
    private bool isCountingDown = false;
    private bool isActivated = true;
    
    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.AddListener<ActivateHMDConfigurationEvent>(ActivateHMDConfigurationEventHandler);
        EventManager.Instance.AddListener<DeactivateHMDConfigurationEvent>(DeactivateHMDConfigurationEventHandler);
    }

    private void DeactivateHMDConfigurationEventHandler(DeactivateHMDConfigurationEvent e)
    {
        isActivated = false;
        _lineRenderer.enabled = false;
    }

    private void ActivateHMDConfigurationEventHandler(ActivateHMDConfigurationEvent e)
    {
        isActivated = true;
        _lineRenderer.enabled = true;
    }

    private void StartOrStopRecording()
    {
        if (!isActivated)
        {
            return;
        }

        if (!isPlaying && !isCountingDown)
        {
            isCountingDown = true;
            StartCoroutine(CountdownAndStartRecording());
        }
    }

    private void StartOrStopPlayer()
    {
        if (!isActivated)
        {
            return;
        }

        if (!isCountingDown)
        {
            StartOrStopPlaying();
        }
    }

    private void PresentationStartEventHandler(PresentationStartEvent e)
    {
        StartOrStopRecording();
    }

    private void PresentationFinishEventHandler(PresentationFinishEvent e)
    {
        StartOrStopRecording();
    }

    private void ViewModeStartEventHandler(ViewModeStartEvent e)
    {
        StartOrStopPlayer();
    }

    private void ViewModeFinishEventHandler(ViewModeFinishEvent e)
    {
        StartOrStopPlayer();
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.RemoveListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.RemoveListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.RemoveListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.RemoveListener<ActivateHMDConfigurationEvent>(ActivateHMDConfigurationEventHandler);
        EventManager.Instance.RemoveListener<DeactivateHMDConfigurationEvent>(DeactivateHMDConfigurationEventHandler);
    }

    void Awake()
    {
        instance = this;
        SubscribeEvents();
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        // Create a line renderer for this object
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.SetVertexCount(2);
        _lineRenderer.material = BoneMaterial;
        _lineRenderer.SetWidth(0.05f, 0.05f);

        saverGaze = GazeRayRecorder.Instance;
    }

    // Update is called once per frame
    private void Update()
    {
        if (!isActivated)
        {
            return;
        }

        if (!isPlaying)
        {
            // Get eye tracking data in world space
            TobiiXR_EyeTrackingData eyeTrackingData = TobiiXR.GetEyeTrackingData(TobiiXR_TrackingSpace.World);


            // Check if gaze ray is valid
            if (eyeTrackingData.GazeRay.IsValid)
            {
                // The origin of the gaze ray is a 3D point
                rayOrigin = eyeTrackingData.GazeRay.Origin;

                // The direction of the gaze ray is a normalized direction vector
                rayDirection = eyeTrackingData.GazeRay.Direction;

                RenderEyeRays();
            }
        }

        if (isRecording && !saverGaze.IsRecording())
        {
            // recording stopped
            isRecording = false;
            _lineRenderer.enabled = true;
        }

        if (isPlaying && !saverGaze.IsPlaying())
        {
            // playing stopped
            isPlaying = false;
            _lineRenderer.enabled = true;
        }
    }

    private void RenderEyeRays()
    {
        Color lrColor = Color.green;

        Ray ray = new Ray();
        ray.origin = rayOrigin;
        ray.direction = rayDirection;

        RaycastHit hit;
        // Does the ray intersect any objects excluding the player layer
        if (Physics.Raycast(ray, out hit))
        {
            _lineRenderer.SetPosition(0, rayOrigin);
            _lineRenderer.SetPosition(1, hit.point);
            _lineRenderer.startColor = lrColor;
            _lineRenderer.endColor = lrColor;
        }

        SetEyePosition();
    }

    private void SetEyePosition()
    {
        Vector3 rayOD = rayDirection - rayOrigin;

        // Calculate the angle from the forward of the root of the eyes and the eye direction
        float angle = Vector3.SignedAngle(eyes.transform.forward, rayOD, eyes.transform.right);
        if(rayDirection.x < 0)
        {
            angle = -angle;
        }

        // Test which percent of the max eye angle
        float percentage = (100 * angle) / maxEyeAngle;

        // Apply this percentage in the range of the eye position
        float x = maxRange * (percentage / 100);
        eyes.transform.localPosition = new Vector3(x, 0, 0);
    }

    // counts down (from 3 for instance), then starts recording
    private IEnumerator CountdownAndStartRecording()
    {
        // count down
        if (!isRecording && countdown != null && countdown.Length > 0)
        {
            for (int i = 0; i < countdown.Length; i++)
            {
                if (countdown[i])
                    countdown[i].gameObject.SetActive(true);

                yield return new WaitForSeconds(1f);

                if (countdown[i])
                    countdown[i].gameObject.SetActive(false);
            }
        }

        isCountingDown = false;

        if (saverGaze)
        {
            if (!isRecording)
            {
                // start recording
                isRecording = true;

                saverGaze.StartRecording();
                _lineRenderer.enabled = false;
            }
            else
            {
                // stop recording
                isRecording = false;

                saverGaze.StopRecordingOrPlaying();
                _lineRenderer.enabled = true;
            }
        }
    }

    // start or stop re-playing
    private void StartOrStopPlaying()
    {
        if (saverGaze)
        {
            if (!isPlaying)
            {
                // start playing
                isPlaying = true;

                saverGaze.StartPlaying();
                _lineRenderer.enabled = true;
            }
            else
            {
                // stop playing
                isPlaying = false;

                saverGaze.StopRecordingOrPlaying();
                _lineRenderer.enabled = true;
            }
        }
    }

    public Vector3 GetRayOrigin()
    {
        return rayOrigin;
    }

    public Vector3 GetRayDirection()
    {
        return rayDirection;
    }

    public void SetGazeData(string data)
    {
        string[] alCsvParts = data.Split(';');

        if (alCsvParts.Length < 6)
            return;

        float.TryParse(alCsvParts[0], out float oX);
        float.TryParse(alCsvParts[1], out float oY);
        float.TryParse(alCsvParts[2], out float oZ);
        float.TryParse(alCsvParts[3], out float dX);
        float.TryParse(alCsvParts[4], out float dY);
        float.TryParse(alCsvParts[5], out float dZ);

        rayOrigin = new Vector3(oX, oY, oZ);
        rayDirection = new Vector3(dX, dY, dZ);

        RenderEyeRays();
    }
}
