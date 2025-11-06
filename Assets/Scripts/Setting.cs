using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Audio;  // สำหรับจัดการ AudioMixer
using UnityEngine.SceneManagement;  // สำหรับจัดการฉาก

public class Setting : MonoBehaviour
{
    public Slider musicSlider;  // สไลด์บาร์สำหรับปรับระดับเสียงดนตรี
    public Slider sfxSlider;  // สไลด์บาร์สำหรับปรับระดับเสียงเอฟเฟกต์
    public TMP_Dropdown backgroundDropdown;  // Dropdown สำหรับเลือกภาพพื้นหลัง
    public Button backButton;  // ปุ่มย้อนกลับ

    public AudioMixer audioMixer;  // ตัวจัดการเสียงด้วย AudioMixer
    public AudioSource musicSource;  // แหล่งเสียงสำหรับเล่นเพลงพื้นหลัง
    public Image backgroundImage;  // ตัวแสดงภาพสำหรับเปลี่ยนภาพพื้นหลัง
    public List<Sprite> backgroundSprites;  // ลิสต์เก็บ Sprite ของภาพพื้นหลัง

    void Start()
    {
        // ตั้งค่า Dropdown สำหรับ Background
        if (backgroundDropdown != null)
        {
            backgroundDropdown.ClearOptions();
            List<string> options = new List<string>() { "Background 1", "Background 2", "Background 3" };
            backgroundDropdown.AddOptions(options);
            backgroundDropdown.onValueChanged.AddListener(delegate { SetBackground(); }); 
        }
        // ตั้งค่า Music Slider
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
            audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicSlider.value) * 20);
            musicSlider.onValueChanged.AddListener(delegate { SetMusicVolume(); });
            if (musicSource != null)
            {
                musicSource.volume = musicSlider.value;
                musicSource.Play();
            }
        }

        // ตั้งค่า SFX Slider
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxSlider.value) * 20);

            sfxSlider.onValueChanged.AddListener(delegate { SetSFXVolume(); });
        }

        ApplyBackground();
        if (backButton != null)
        {
            backButton.onClick.AddListener(GoBack);
        }
    }
    // ฟังก์ชันสำหรับปรับระดับเสียงดนตรี
    public void SetMusicVolume()
    {
        if (musicSlider != null)
        {
            float volume = Mathf.Log10(musicSlider.value) * 20;
            audioMixer.SetFloat("MusicVolume", volume);
            PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
            
            if (musicSource != null)
            {
                musicSource.volume = musicSlider.value;
            }
        }
    }
    // ฟังก์ชันสำหรับปรับระดับเสียงเอฟเฟกต์
    public void SetSFXVolume()
    {
        if (sfxSlider != null)
        {
            float volume = Mathf.Log10(sfxSlider.value) * 20;
            audioMixer.SetFloat("SFXVolume", volume);
            PlayerPrefs.SetFloat("SFXVolume", sfxSlider.value);
        }
    }
    // ฟังก์ชันสำหรับการตั้งค่าภาพพื้นหลังจากการเลือกใน Dropdown
    public void SetBackground()
    {
        PlayerPrefs.SetInt("BackgroundChoice", backgroundDropdown.value);
        ApplyBackground();
    }
    // ฟังก์ชันสำหรับเปลี่ยนภาพพื้นหลังตามตัวเลือกที่ผู้ใช้เลือก
    void ApplyBackground()
    {
        int backgroundChoice = PlayerPrefs.GetInt("BackgroundChoice", 0);

        if (backgroundChoice >= 0 && backgroundChoice < backgroundSprites.Count)
        {
            backgroundImage.sprite = backgroundSprites[backgroundChoice];
        }
    }
    // ฟังก์ชันสำหรับย้อนกลับไปยังหน้าก่อนหน้า
    void GoBack()
    {
        // โหลดฉากใหม่และให้การตั้งค่าที่ทำไว้มีผล
       SceneManager.LoadScene("First_Sceme");

    }
}
