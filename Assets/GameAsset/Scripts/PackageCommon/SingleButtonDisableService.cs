using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
[System.Serializable]
public class SingleButtonDisableService
{
    [SerializeField] private List<Button> buttons = new();

    public void Initialize()
    {
        RegisterEventsForAllButtons();
    }
    public SingleButtonDisableService AddAndRegisterButtons(params Button[] newButtons)
    {
        foreach (var button in newButtons)
        {
            if (buttons.Contains(button))
            {
                continue;
            }
            buttons.Add(button);
            RegisterEvent(button);
        }
        return this;
    }
    private void RegisterEvent(Button button)
    {
        button.onClick.AddListener(() =>
        {
            DisableAllButtonsExcept(button);
        });
    }
    public void RegisterEventsForAllButtons()
    {
        foreach (var button in buttons)
        {
            RegisterEvent(button);
        }
    }
    private void DisableAllButtonsExcept(Button selectedButton)
    {
        buttons.ForEach(x => x.interactable = true);
        selectedButton.interactable = false;
    }
    public void EnableAllButtons()
    {
        buttons.ForEach(x => x.interactable = true);
    }

    public void ClickButtonByIndex(int index)
    {
        if (index >= 0 && index < buttons.Count)
        {
            buttons[index].onClick.Invoke();
        }
    }

    public void ClickFirstButton()
    {
        ClickButtonByIndex(0);
    }
    public bool IsButtonInteractable(Button button)
    {
        return buttons.Contains(button) && button.interactable;
    }
    public List<Button> GetManagedButtons()
    {
        return buttons;
    }
    public Button GetSelectedButton()
    {
        return buttons.Find(btn => !btn.interactable);
    }

    public void Clear()
    {
        buttons.Clear();
    }
    
    public void ClickFirstEnableButton()
    {
        var button = buttons.Find(btn => btn.gameObject.activeSelf);
        if (button != null)
        {
            button.onClick.Invoke();
        }
    }

    public void ResetToEnableFirstButton()
    {
        if (buttons != null && buttons.Count > 0)
        {
            DisableAllButtonsExcept(buttons[0]);
        }
    }
    public int GetButtonCount() { return buttons.Count; }
}
}
