using SDD.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadSetPosition : MonoBehaviour
{
    public GameObject headSetPosition;
    private Vector3 initialPosition;

    private bool isPlaying;

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.AddListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    private void ViewModeStartEventHandler(ViewModeStartEvent e)
    {
        isPlaying = true;
    }

    private void ViewModeFinishEventHandler(ViewModeFinishEvent e)
    {
        isPlaying = false;
    }

    private void EndViewModelEventHandler(EndViewModeEvent e)
    {
        isPlaying = false;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.RemoveListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.RemoveListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    void Awake()
    {
        isPlaying = false;
        SubscribeEvents();
        initialPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlaying)
        {
            transform.position = headSetPosition.transform.position;
        }
        else
        {
            transform.position = initialPosition;
        }
    }

    void OnDestroy()
    {
        CancelEvents();
    }
}
