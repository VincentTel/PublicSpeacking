using VRTK;
using UnityEngine;
using System;
using SDD.Events;
using System.Collections;
using UnityEngine.UI;

public class PresentationManager : MonoBehaviour
{
    [Header("Presentation support")]
    [SerializeField] private GameObject[] _presentationSupports;

    [Header("Technical details")]
    [SerializeField] private GameObject _leftController;
    [SerializeField] private GameObject _rightController;
    [SerializeField] private float _buttonCooldown = 2;

    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;
    public GameObject recordImage = null;
    public GameObject playImage = null;

    private Texture2D[] _slidesTextures;
    private int _currentSlide;
    private VRTK_ControllerEvents _rightControllerEvents;
    private VRTK_ControllerEvents _leftControllerEvents;
    protected static PresentationManager instance = null;
    SlidesRecorder slideRecorder = null;

    // recording parameters
    private bool isRecording = false;
    private bool isPlaying = false;
    private bool isCountingDown = false;
    private float timeTriggered;

    private string filePath;

    public static PresentationManager Instance
    {
        get
        {
            return instance;
        }
    }

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<SetPathEvents>(SetPathEventsHandler);
        EventManager.Instance.AddListener<SetSlidesTextureEvents>(SetSlidesTextureEventsHandler);
    }

    private void SetSlidesTextureEventsHandler(SetSlidesTextureEvents e)
    {
        _slidesTextures = e.textures;
    }

    private void SetPathEventsHandler(SetPathEvents e)
    {
        filePath = e.Path;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<SetPathEvents>(SetPathEventsHandler);
        EventManager.Instance.RemoveListener<SetSlidesTextureEvents>(SetSlidesTextureEventsHandler);
    }

    void Awake()
    {
        filePath = "";
        SubscribeEvents();
        instance = this;
        timeTriggered = Time.time;
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        _currentSlide = -1;

        _rightControllerEvents = _rightController.GetComponent<VRTK_ControllerEvents>();
        _leftControllerEvents = _leftController.GetComponent<VRTK_ControllerEvents>();

        if (!slideRecorder)
        {
            slideRecorder = SlidesRecorder.Instance;
        }
        else
        {
            Debug.Log("SlidesRecorder not found, probably not initialized.");
        }

        EventsMapping();
        recordImage.SetActive(false);
        playImage.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (isRecording)
        {
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                NextSlide();
            }
            else if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.PageUp) ||
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                PreviousSlide();
            }
        }

        if (isRecording && !slideRecorder.IsRecording())
        {
            // recording stopped
            isRecording = false;
            recordImage.SetActive(false);
        }

        if (isPlaying && !slideRecorder.IsPlaying())
        {
            // playing stopped
            isPlaying = false;
            playImage.SetActive(false);
            EventManager.Instance.Raise(new EndViewModeEvent());
        }

    }

    private void EventsMapping()
    {
        if (_rightControllerEvents == null || _leftControllerEvents == null)
        {
            Debug.LogError("Need to have VRTK_ControllerEvents component attached to the controller");
            return;
        }

        _rightControllerEvents.TriggerClicked += new ControllerInteractionEventHandler(RightControllerTriggerPress);
        _leftControllerEvents.TriggerClicked += new ControllerInteractionEventHandler(LeftControllerTriggerPress);
        _leftControllerEvents.GripClicked += new ControllerInteractionEventHandler(LeftControllerGripClickedPress);
        _rightControllerEvents.GripClicked += new ControllerInteractionEventHandler(RightControllerGripClickedPress);
    }

    private void LeftControllerGripClickedPress(object sender, ControllerInteractionEventArgs e)
    {
        //StartAndStopViewMode();
    }

    private void RightControllerGripClickedPress(object sender, ControllerInteractionEventArgs e)
    {
        PreviousSlide();
    }

    private void LeftControllerTriggerPress(object sender, ControllerInteractionEventArgs e)
    {
        //StartAndStopPresentation();
    }

    private void RightControllerTriggerPress(object sender, ControllerInteractionEventArgs e)
    {
        NextSlide();
    }

    private void PreviousSlide()
    {
        --_currentSlide;
        SetSlide();
    }

    private void NextSlide()
    {
        ++_currentSlide;
        SetSlide();
    }

    public void StartAndStopViewMode()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        if (filePath == "")
        {
            Debug.LogError("No path specified. Impossible to load the record");
            return;
        }

        if (!isPlaying)
        {
            EventManager.Instance.Raise(new ViewModeStartEvent());
        }
        else
        {
            EventManager.Instance.Raise(new ViewModeFinishEvent());
        }

        if (!isCountingDown)
        {
            StartOrStopPlaying();
        }
    }

    public void StartAndStopPresentation()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        if (filePath == "")
        {
            Debug.LogError("No path specified. Impossible to save the record");
            return;
        }

        if (!isRecording)
        {
            EventManager.Instance.Raise(new PresentationStartEvent());
        }
        else
        {
            EventManager.Instance.Raise(new PresentationFinishEvent());
        }

        StartOrStopRecording();
    }

    public int GetCurrentSlide()
    {
        return _currentSlide;
    }

    public void SetSlide()
    {
        if (_presentationSupports.Length == 0)
        {
            _currentSlide = -1;
            return;
        }

        // We are at the first slide
        if (_currentSlide <= 0)
        {
            _currentSlide = 0;
        }
        // We are at the last slide
        else if (_currentSlide >= _slidesTextures.Length - 1)
        {
            _currentSlide = _slidesTextures.Length - 1;
        }

        Material slideMaterial = new Material(Shader.Find("Unlit/Texture"));
        slideMaterial.mainTexture = _slidesTextures[_currentSlide];
        slideMaterial.mainTextureScale = new Vector2(1, -1);

        foreach (GameObject presentationSupport in _presentationSupports)
        {
            Renderer tmpRender = presentationSupport.GetComponent<Renderer>();
            RawImage tmpRawImage = presentationSupport.GetComponent<RawImage>();

            if (tmpRender)
            {
                tmpRender.material = slideMaterial;
            }
            else if (tmpRawImage)
            {
                tmpRawImage.material = slideMaterial;
            }

        }
    }

    public void SetSlidesData(string data)
    {
        string[] alCsvParts = data.Split(';');

        if (alCsvParts.Length < 1)
            return;

        int.TryParse(alCsvParts[0], out int slideNumber);

        if (slideNumber == _currentSlide)
        {
            return;
        }

        _currentSlide = slideNumber;

        SetSlide();
    }

    private void StartOrStopRecording()
    {
        if (!isPlaying && !isCountingDown)
        {
            isCountingDown = true;
            StartCoroutine(CountdownAndStartRecording());
        }
    }

    // start or stop re-playing
    private void StartOrStopPlaying()
    {
        if (slideRecorder)
        {
            if (!isPlaying)
            {
                // start playing
                isPlaying = true;

                slideRecorder.StartPlaying();
                playImage.SetActive(true);
            }
            else
            {
                // stop playing
                isPlaying = false;

                slideRecorder.StopRecordingOrPlaying();
                playImage.SetActive(false);
            }
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

        if (slideRecorder)
        {
            if (!isRecording)
            {
                // start recording
                isRecording = true;

                slideRecorder.StartRecording();
                recordImage.SetActive(true);
            }
            else
            {
                // stop recording
                isRecording = false;

                slideRecorder.StopRecordingOrPlaying();
                recordImage.SetActive(false);
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (isRecording)
        {
            EventManager.Instance.Raise(new PresentationFinishEvent());
            // recording stopped
            isRecording = false;
            recordImage.SetActive(false);
        }

        if (isPlaying)
        {
            EventManager.Instance.Raise(new ViewModeFinishEvent());
            // playing stopped
            isPlaying = false;
            playImage.SetActive(false);
            //EventManager.Instance.Raise(new EndViewModeEvent());
        }
    }
}
