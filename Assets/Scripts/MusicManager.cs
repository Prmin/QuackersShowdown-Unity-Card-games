using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        PlayMusic();
    }

    public void PlayMusic()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play(); // เริ่มเล่นเพลง
        }
    }

    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop(); // หยุดเล่นเพลง
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = volume; // ปรับระดับเสียง
        }
    }
}

