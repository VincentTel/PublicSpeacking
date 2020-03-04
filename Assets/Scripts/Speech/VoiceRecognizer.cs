using SDD.Events;
using System.Collections;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceRecognizer : MonoBehaviour
{
    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;

    private DictationRecognizer m_DictationRecognizer;
    // reference to BodyDataRecorderPlayer
    private VoiceRecorder voiceRecorder;
    private string speech;

    // recording parameters
    private bool isRecording = false;
    private bool isCountingDown = false;
    protected static VoiceRecognizer instance = null;

    public static VoiceRecognizer Instance
    {
        get
        {
            return instance;
        }
    }

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
    }

    private void StartOrStopRecording()
    {
        if (!isCountingDown)
        {
            isCountingDown = true;
            speech = "";
            StartCoroutine(CountdownAndStartRecording());
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

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.RemoveListener<PresentationFinishEvent>(PresentationFinishEventHandler);
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

    //Call to each speech recognition event to be sure that all sentences are taken in the script
    private void StoptheRecord()
    {
        if (m_DictationRecognizer.Status == SpeechSystemStatus.Stopped)
        {
            voiceRecorder.StopRecordingOrPlaying();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!voiceRecorder)
        {
            voiceRecorder = VoiceRecorder.Instance;
        }

        m_DictationRecognizer = new DictationRecognizer();

        m_DictationRecognizer.DictationResult += (text, confidence) =>
        {
            //Debug.LogFormat("Dictation result: {0}", text);
            speech += text;

            StoptheRecord();
        };

        m_DictationRecognizer.DictationHypothesis += (text) =>
        {
            //Debug.LogFormat("Dictation hypothesis: {0}", text);
            //speech += text;
            //StoptheRecord();
        };

        m_DictationRecognizer.DictationComplete += (completionCause) =>
        {
            if (completionCause != DictationCompletionCause.Complete)
                Debug.LogErrorFormat("Dictation completed unsuccessfully: {0}.", completionCause);

            StoptheRecord();
        };

        m_DictationRecognizer.DictationError += (error, hresult) =>
        {
            Debug.LogErrorFormat("Dictation error: {0}; HResult = {1}.", error, hresult);

            StoptheRecord();
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (!voiceRecorder)
            return;

        if (isRecording && !voiceRecorder.IsRecording())
        {
            // recording stopped
            isRecording = false;
        }
    }
    
    // counts down (from 3 for instance), then starts recording
    private IEnumerator CountdownAndStartRecording()
    {
        // count down
        if (!isRecording && countdown != null && countdown.Length > 0)
        {
            Debug.LogError("Boucle countdown");

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

        if (voiceRecorder)
        {
            if (!isRecording)
            {
                // start recording
                isRecording = true;
                m_DictationRecognizer.Start();
                voiceRecorder.StartRecording();
            }
            else
            {
                // stop recording
                isRecording = false;
                m_DictationRecognizer.Stop();
            }
        }
    }

    public string GetTextRecognized()
    {
        // Clear the recognized to not send twice the same data
        string tmp = speech;
        speech = "";
        return tmp;
    }

    public void SetTextToSpeech(string toSpeech)
    {
        speech = toSpeech; //TODO::implement the text to voice
    }
}
