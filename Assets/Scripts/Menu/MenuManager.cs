using SDD.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject pauseButton;
    public GameObject hmdButton;
    public GameObject feedBack;

    public Sprite playIcon;
    public Sprite pauseIcon;
    public Sprite checkBoxEmpty;
    public Sprite checkBox;

    public float _buttonCooldown = 3;

    private bool _menuDisplayed;
    private bool _hmdUsability;

    private bool isPlaying;
    private bool isRecording;

    private float timeTriggered;

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.AddListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    private void EndViewModelEventHandler(EndViewModeEvent e)
    {
        ShowMenu();
    }

    private void PresentationStartEventHandler(PresentationStartEvent e)
    {
        HideMenu();
    }

    private void PresentationFinishEventHandler(PresentationFinishEvent e)
    {
        ShowMenu();
    }

    private void ViewModeStartEventHandler(ViewModeStartEvent e)
    {
        HideMenu();
    }

    private void ViewModeFinishEventHandler(ViewModeFinishEvent e)
    {
        ShowMenu();
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.RemoveListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.RemoveListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.RemoveListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.RemoveListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    private void ShowPanel()
    {
        if (mainMenuPanel == null)
        {
            return;
        }

        _menuDisplayed = true;
        mainMenuPanel.SetActive(true);
    }

    private void HidePanel()
    {
        if (mainMenuPanel == null)
        {
            return;
        }

        _menuDisplayed = false;
        mainMenuPanel.SetActive(false);
    }

    private void ShowButton()
    {
        if (pauseButton == null)
        {
            return;
        }

        pauseButton.SetActive(true);
    }

    private void HideButton()
    {
        if (pauseButton == null)
        {
            return;
        }

        pauseButton.SetActive(false);
    }

    private void ShowFeedback()
    {
        if (feedBack == null)
        {
            return;
        }

        feedBack.SetActive(true);
    }

    private void HideFeedback()
    {
        if (feedBack == null)
        {
            return;
        }

        feedBack.SetActive(false);
    }

    private void HideMenu()
    {
        ShowButton();
        ShowFeedback();
        HidePanel();
        ModifyPauseButton();
    }

    private void ShowMenu()
    {
        ShowPanel();
        HideButton();
        HideFeedback();
        ModifyPauseButton();
    }

    private void ShowMenuAndButton()
    {
        ShowPanel();
        ShowButton();
        HideFeedback();
        ModifyPauseButton();
    }

    private void ModifyPauseButton()
    {
        if (pauseButton == null)
        {
            return;
        }

        if (_menuDisplayed)
        {
            pauseButton.GetComponentInChildren<Image>().sprite = playIcon;
        }
        else
        {
            pauseButton.GetComponentInChildren<Image>().sprite = pauseIcon;
        }
    }

    void Awake()
    {
        SubscribeEvents();
        _menuDisplayed = true;
        _hmdUsability = true;
        isPlaying = false;
        isRecording = false;
        timeTriggered = Time.time;
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    public void PlayPauseButton()
    {
        if (_menuDisplayed)
        {
            HideMenu();
        }
        else
        {
            ShowMenuAndButton();
        }
        ModifyPauseButton();
    }

    public void ChangePlayingStatement()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        isPlaying = !isPlaying;
    }

    public void ChangeRecordingStatement()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        isRecording = !isRecording;
    }

    public void ChangeDisplayHMDButton()
    {
        if (hmdButton == null || isPlaying || isRecording)
        {
            return;
        }

        if (_hmdUsability)
        {
            hmdButton.GetComponent<Image>().sprite = checkBoxEmpty;
            _hmdUsability = false;
            EventManager.Instance.Raise(new DeactivateHMDConfigurationEvent());
        }
        else
        {
            hmdButton.GetComponent<Image>().sprite = checkBox;
            _hmdUsability = true;
            EventManager.Instance.Raise(new ActivateHMDConfigurationEvent());
        }
    }

    public void ApplicationQuit()
    {
        Application.Quit();
    }
}
