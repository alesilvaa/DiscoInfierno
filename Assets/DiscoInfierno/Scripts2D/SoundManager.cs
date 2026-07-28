using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance; // Singleton para acceso global

    public AudioSource effectsSource;   // Fuente para efectos de sonido
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip winsound;      
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip soundWater;
    private bool once;

    private void Awake()
    {
        Application.targetFrameRate = 60; // Fijar la tasa de refresco a 60 FPS
        
        // Implementar el patrón Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persistir entre escenas
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaySoundJump()
    {
        effectsSource.PlayOneShot(_jumpSound);
    }
    
    public void PlayWinSound()
    {
        effectsSource.PlayOneShot(winsound);
    }
    
    public void PlayClickSound()
    {
        effectsSource.PlayOneShot(clickSound);
    }
    
    public void PlayWaterSound()
    {
        effectsSource.PlayOneShot(soundWater);
    }
    
}