using SDD.Events;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class AudienceMemberReaction : MonoBehaviour
{
    private enum ScoreState
    {
        Good,
        OK,
        Bad
    }

    [Range(0, 100)]
    public int animationRate = 5;
    public float minTimeLoopDuration = 5.0f;

    public string[] animationStatesGood;
    public string[] animationStatesOK = { "Default" };
    public string[] animationStatesBad;

    private Animator animator = null;
    private ScoreState scoreState = ScoreState.OK;

    private float lastAnimationTime;
    
    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<SetGoodScoreEvent>(SetGoodScoreEventHandler);
        EventManager.Instance.AddListener<SetOKScoreEvent>(SetOKScoreEventHandler);
        EventManager.Instance.AddListener<SetBadScoreEvent>(SetBadScoreEventHandler);
    }

    private void SetBadScoreEventHandler(SetBadScoreEvent e)
    {
        scoreState = ScoreState.Bad;
    }

    private void SetOKScoreEventHandler(SetOKScoreEvent e)
    {
        scoreState = ScoreState.OK;
    }

    private void SetGoodScoreEventHandler(SetGoodScoreEvent e)
    {
        scoreState = ScoreState.Good;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<SetGoodScoreEvent>(SetGoodScoreEventHandler);
        EventManager.Instance.RemoveListener<SetOKScoreEvent>(SetOKScoreEventHandler);
        EventManager.Instance.RemoveListener<SetBadScoreEvent>(SetBadScoreEventHandler);
    }

    private void OnDestroy()
    {
        CancelEvents();
    }

    private void Awake()
    {
        // Allow animations right now
        lastAnimationTime = Time.time - minTimeLoopDuration;
        SubscribeEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        animator = gameObject.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("No animator Finded, member " + gameObject.name + " is not initialised");
        }
        else
        {
            Debug.Log(gameObject.name + " initialized");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!CheckAnimationRate() || !CheckCoolDown())
        {
            return;
        }

        lastAnimationTime = Time.time; //Set here to avoid 2 updates in a row
        TriggerNextState();

        switch (scoreState)
        {
            case ScoreState.Good:
                RunGoodAnimation();
                break;
            case ScoreState.OK:
                RunOKAnimation();
                break;
            case ScoreState.Bad:
                RunBadAnimation();
                break;
        }
    }

    private bool CheckCoolDown()
    {
        if (Time.time > lastAnimationTime + minTimeLoopDuration)
        {
            return true;
        }
        return false;
    }

    private void TriggerNextState()
    {
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Loop"))
        {
            animator.SetTrigger("NextState");
        }
    }

    private bool CheckAnimationRate()
    {
        if (Random.Range(0, 100) < animationRate)
        {
            return true;
        }

        return false;
    }

    private void RunGoodAnimation()
    {
        if (animationStatesGood.Length == 0)
        {
            return;
        }

        RunAnimation(animationStatesGood[Random.Range(0, animationStatesGood.Length)]);
    }

    private void RunOKAnimation()
    {
        if (animationStatesOK.Length == 0)
        {
            return;
        }

        RunAnimation(animationStatesOK[Random.Range(0, animationStatesOK.Length)]);
    }

    private void RunBadAnimation()
    {
        if (animationStatesBad.Length == 0)
        {
            return;
        }

        RunAnimation(animationStatesBad[Random.Range(0, animationStatesBad.Length)]);
    }

    private void RunAnimation(string state)
    {
        StartCoroutine(WaitEndAnimation(state));
    }

    private IEnumerator WaitEndAnimation(string state)
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);

        lastAnimationTime = Time.time; //Set it again here to have the correct time
        animator.Play(state);
    }
}
