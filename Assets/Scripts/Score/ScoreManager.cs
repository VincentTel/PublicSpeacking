using SDD.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{

    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;
    // Temporary before having audiance
    public GameObject good;
    public GameObject ok;
    public GameObject bad;

    public float maxScore = 100;
    private float score;

    // Instance of score
    protected static ScoreManager instance;

    private ScoreRecorder scoreRecorder;

    // recording parameters
    private bool isRecording = false;
    private bool isPlaying = false;
    private bool isCountingDown = false;

    public static ScoreManager Instance
    {
        get { return instance; }
    }

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
    }

    private void StartOrStopRecording()
    {
        if (!isPlaying && !isCountingDown)
        {
            isCountingDown = true;
            StartCoroutine(CountdownAndStartRecording());
        }
    }

    private void StartOrStopPlayer()
    {
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
    }

    private void Awake()
    {
        instance = this;
        good.SetActive(false);
        ok.SetActive(false);
        bad.SetActive(false);
        score = 0;
        SubscribeEvents();
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (scoreRecorder == null)
        {
            scoreRecorder = ScoreRecorder.Instance;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPlaying)
        {
            SetFinalScore();
        }

        if (isRecording && !scoreRecorder.IsRecording())
        {
            // recording stopped
            isRecording = false;
        }

        if (isPlaying && !scoreRecorder.IsPlaying())
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

        if (scoreRecorder)
        {
            if (!isRecording)
            {
                // start recording
                isRecording = true;

                scoreRecorder.StartRecording();
            }
            else
            {
                // stop recording
                isRecording = false;

                scoreRecorder.StopRecordingOrPlaying();
            }
        }
    }

    // start or stop re-playing
    private void StartOrStopPlaying()
    {
        if (scoreRecorder)
        {
            if (!isPlaying)
            {
                // start playing
                isPlaying = true;

                scoreRecorder.StartPlaying();
            }
            else
            {
                // stop playing
                isPlaying = false;

                scoreRecorder.StopRecordingOrPlaying();
            }
        }
    }

    private void SetFinalScore()
    {
        if (score < maxScore / 3)
        {
            SetBadScore();
        }
        else if (score < (maxScore / 3) * 2)
        {
            SetOkScore();
        }
        else if (score < maxScore)
        {
            SetGoodScore();
        }
    }

    public float GetMaxScore()
    {
        return maxScore;
    }

    public void SetScore(float newScore)
    {
        score = newScore;
    }

    public float GetScore()
    {
        return score;
    }

    private void SetGoodScore()
    {
        good.SetActive(true);
        ok.SetActive(false);
        bad.SetActive(false);
        EventManager.Instance.Raise(new SetGoodScoreEvent());
    }

    private void SetOkScore()
    {
        good.SetActive(false);
        ok.SetActive(true);
        bad.SetActive(false);
        EventManager.Instance.Raise(new SetOKScoreEvent());
    }

    private void SetBadScore()
    {
        good.SetActive(false);
        ok.SetActive(false);
        bad.SetActive(true);
        EventManager.Instance.Raise(new SetBadScoreEvent());
    }

    public void SetScoreData(string data)
    {
        string[] alCsvParts = data.Split(';');

        if (alCsvParts.Length < 1)
            return;

        float.TryParse(alCsvParts[0], out float lscore);

        score = lscore;

        SetFinalScore();
    }
}
