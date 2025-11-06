using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerCountDropdown : MonoBehaviour
{
    public TMP_Dropdown playerCountDropdown;
    void Start()
    {
        List<string> options = new List<string> { "3 Players", "4 Players", "5 Players", "6 Players" };
        playerCountDropdown.ClearOptions();
        playerCountDropdown.AddOptions(options);
        playerCountDropdown.onValueChanged.AddListener(delegate { OnDropdownValueChanged(playerCountDropdown); });
    }
    void OnDropdownValueChanged(TMP_Dropdown dropdown)
    {
        int selectedValue = dropdown.value + 3; 
        Debug.Log("Selected player count: " + selectedValue);
    }
}