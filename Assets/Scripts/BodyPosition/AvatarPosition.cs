using com.rfilkov.components;
using com.rfilkov.kinect;
using SDD.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AvatarPosition : MonoBehaviour
{
    public GameObject positionReference;
    [Range(-1f, 1f)]
    public float offsetZ = -0.23f;
    [Range(-1f, 1f)]
    public float offsetX = 0.1f;
    public float BaseUnitMetric = .5f;
    public GameObject kinectRepresentation = null;
    public GameObject boardPresentation = null;

    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;

    Vector3 position;
    BodyPositionRecorder saverPlayerPosition;
    protected static AvatarPosition instance = null;
    AvatarController avatarController = null;
    KinectManager kinectManager = null;
    private bool isActivated = true;
    private float unitMetric;

    public static AvatarPosition Instance
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

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.AddListener<ActivateHMDConfigurationEvent>(ActivateHMDConfigurationEventHandler);
        EventManager.Instance.AddListener<DeactivateHMDConfigurationEvent>(DeactivateHMDConfigurationEventHandler);
    }

    public void CalibrateMaxPosition()
    {
        ulong userId = GetKinectUserId();
        Vector3 userPosition = kinectManager.GetJointPosition(userId, KinectInterop.JointType.Pelvis);

        float virtualMaxZ = Mathf.Abs(kinectRepresentation.transform.position.z - boardPresentation.transform.position.z);
        float realMaxZ = Mathf.Abs(userPosition.z);

        unitMetric = 1 / (realMaxZ / virtualMaxZ);
    }

    private void DeactivateHMDConfigurationEventHandler(DeactivateHMDConfigurationEvent e)
    {
        isActivated = false;
        avatarController.externalRootMotion = false;
    }

    private void ActivateHMDConfigurationEventHandler(ActivateHMDConfigurationEvent e)
    {
        isActivated = true;
        avatarController.externalRootMotion = true;
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
        unitMetric = 1;
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (saverPlayerPosition == null)
        {
            saverPlayerPosition = BodyPositionRecorder.Instance;
        }

        if (kinectManager == null)
        {
            kinectManager = KinectManager.Instance;
        }

        if (avatarController == null)
        {
            avatarController = GetComponent<AvatarController>();
        }
    }

    private ulong GetKinectUserId()
    {
        List<int> userIndices = kinectManager.GetAllUserIndices();
        return kinectManager.GetUserIdByIndex(userIndices.Any() ? userIndices.First() : 0);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isActivated)
        {
            ulong userId = GetKinectUserId();
            Vector3 kinectJoint = kinectManager.GetJointPosition(userId, KinectInterop.JointType.Pelvis);
            position = kinectRepresentation.transform.position + new Vector3(-kinectJoint.x, 0, kinectJoint.z) * unitMetric * -1;
            position.y = 0;
            SetBodyPosition();
            return;
        }

        if (positionReference == null && !isPlaying) return;

        if (!isPlaying)
        {
            Vector3 jointOffset = Vector3.zero;
            if (kinectManager != null)
            {
                List<int> userIndices = kinectManager.GetAllUserIndices();
                ulong userId = kinectManager.GetUserIdByIndex(userIndices.Any() ? userIndices.First() : 0);
                jointOffset = kinectManager.GetJointKinectPosition(userId, KinectInterop.JointType.Nose, false) - 
                //jointOffset = positionReference.transform.position - 
                    kinectManager.GetJointKinectPosition(userId, KinectInterop.JointType.Pelvis, false);
                jointOffset.y = 0;
            }

            position = new Vector3(positionReference.transform.position.x - offsetX, 0, positionReference.transform.position.z - offsetZ) + jointOffset;
            SetBodyPosition();
        }

        if (isRecording && !saverPlayerPosition.IsRecording())
        {
            // recording stopped
            isRecording = false;
        }

        if (isPlaying && !saverPlayerPosition.IsPlaying())
        {
            // playing stopped
            isPlaying = false;
        }

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

        if (saverPlayerPosition)
        {
            if (!isRecording)
            {
                // start recording
                isRecording = true;

                saverPlayerPosition.StartRecording();
            }
            else
            {
                // stop recording
                isRecording = false;

                saverPlayerPosition.StopRecordingOrPlaying();
            }
        }
    }

    // start or stop re-playing
    private void StartOrStopPlaying()
    {
        if (saverPlayerPosition)
        {
            if (!isPlaying)
            {
                // start playing
                isPlaying = true;

                saverPlayerPosition.StartPlaying();
            }
            else
            {
                // stop playing
                isPlaying = false;

                saverPlayerPosition.StopRecordingOrPlaying();
            }
        }
    }

    public Vector3 GetBodyPosition()
    {
        return position;
    }

    public void SetBodyPosition()
    {
        transform.position = position;
    }

    public void SetBodyData(string data)
    {
        string[] alCsvParts = data.Split(';');

        if (alCsvParts.Length < 2)
            return;

        float.TryParse(alCsvParts[0], out float x);
        float.TryParse(alCsvParts[1], out float z);

        position = new Vector3(x, 0, z);

        SetBodyPosition();
    }
}
