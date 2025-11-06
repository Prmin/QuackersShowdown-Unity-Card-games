using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Settings_Manager : MonoBehaviour
{
    public static Settings_Manager instance = null;
    public AudioMixer audioMixer;
    public AudioSource musicSource;
    public float musicVolume = 1.0f;
    public float sfxVolume = 1.0f;
    public int backgroundChoice = 0;
    public Image backgroundImage;
    public List<Sprite> backgroundSprites;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        LoadSettings();
        ApplySettings();
         if (musicSource != null)
    {
        musicSource.enabled = true; 
        musicSource.volume = musicVolume; 
        musicSource.Play();
    }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("MusicVolume", volume);

        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    public void SetBackground(int choice)
    {
        backgroundChoice = choice;
        PlayerPrefs.SetInt("BackgroundChoice", choice);
        ApplyBackground(backgroundImage);
    }

    void LoadSettings()
    {
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        backgroundChoice = PlayerPrefs.GetInt("BackgroundChoice", 0);

        Debug.Log("Number of background sprites: " + backgroundSprites.Count);
        Debug.Log("Current background choice: " + backgroundChoice);
    }

    public void ApplySettings()
    {
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolume) * 20);
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolume) * 20);

       if (musicSource != null)
        {
            musicSource.enabled = true; 
            musicSource.volume = musicVolume;
            musicSource.Play();
            Debug.Log("Playing music...");
        }
        ApplyBackground(backgroundImage);
    }

    void UpdateBackground()
    {
        if (backgroundSprites.Count == 0)
        {
            Debug.LogError("No background sprites assigned!");
            return;
        }
        backgroundChoice = Mathf.Clamp(backgroundChoice, 0, backgroundSprites.Count - 1);
        ApplyBackground(backgroundImage);
    }
    public void ApplyBackground(Image image)
    {
        if (image == null)
        {
            Debug.LogError("Image component is null.");
            return;
        }
        if (backgroundChoice < 0 || backgroundChoice >= backgroundSprites.Count)
        {
            Debug.LogError($"Background choice index is out of range: {backgroundChoice}. Total backgrounds available: {backgroundSprites.Count}");
            return;
        }
        image.sprite = backgroundSprites[backgroundChoice];
        Debug.Log($"Background applied: {backgroundSprites[backgroundChoice].name}");
    }
}
