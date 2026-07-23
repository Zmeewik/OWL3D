using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WeaponWheelUI : MonoBehaviour, IButtonClick
{
    [SerializeField] private GameObject wheelPrefab;
    public event Action<string> OnWeaponChange;
    [SerializeField] private ImageButton[] images;
    [HideInInspector] public string lastWeapon;
    private string selectedWeapon;

    [Serializable]
    struct ImageButton
    {
        public string name;
        public PointerEnterImage image;
    }
    
    private readonly Dictionary<PointerEnterImage, (Action enter, Action exit)> callbacks = new();

    private void OnEnable()
    {
        foreach (var button in images)
        {
            Action enter = () => SelectWeapon(button.name);
            Action exit = () => DeselectWeapon();
            
            callbacks[button.image] = (enter, exit);
            button.image.OnPointerEnterEvent += enter;
            button.image.OnPointerExitEvent += exit;
        }
    }

    private void OnDisable()
    {
        foreach (var button in images)
        {
            if (callbacks.TryGetValue(button.image, out var callback))
            {
                button.image.OnPointerEnterEvent -= callback.enter;
                button.image.OnPointerExitEvent -= callback.exit;
            }
        }

        callbacks.Clear();
    }

    // Wheel interaction
    public void OpenWheel()
    {
        wheelPrefab.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    public void CloseWheel()
    {
        wheelPrefab.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    // Wheel choose
    private void SelectWeapon(string name)
    {
        selectedWeapon = name;
        //print(selectedWeapon);
    }
    
    private void DeselectWeapon()
    {
        selectedWeapon = "";
        //print(selectedWeapon);
    }

    public void ChangeWeapon(bool selection)
    {
        if (selection)
        {
            //print("Wheel opened!");
            OpenWheel();
        }
        else
        {
            //print("Wheel closed!");
            if (selectedWeapon != lastWeapon)
            {
                lastWeapon = selectedWeapon;
                OnWeaponChange?.Invoke(selectedWeapon);
            }
            CloseWheel();
        }
    }

    public void PressButton(string buttonName, bool state)
    {
        //print("Button pressed: " + buttonName);
        if(buttonName == "Wheel")
            ChangeWeapon(state);
    }

}