using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.Audio;

public sealed class MusicPlaylistPlayer : MonoBehaviour
{
    public const float DefaultVolume = 0.5f;
    private const string VolumePreferenceKey = "WrightTycoon.MusicVolume";

    private static readonly string[] TrackResourcePaths =
    {
        "WT04/Music/HypnoticChill",
        "WT04/Music/YoungVisionaries",
        "WT04/Music/RidingSkyWaves",
        "WT04/Music/Network",
        "WT04/Music/ScalingUp",
    };

    private readonly List<AudioClip> _playlist = new();
    private AudioSource? _audioSource;
    private int _trackIndex = -1;

    public float Volume
    {
        get => _audioSource is null ? LoadSavedVolume() : _audioSource.volume;
        set
        {
            var normalizedVolume = Mathf.Clamp01(value);
            if (_audioSource is not null)
            {
                _audioSource.volume = normalizedVolume;
            }

            PlayerPrefs.SetFloat(VolumePreferenceKey, normalizedVolume);
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.volume = LoadSavedVolume();

        for (var index = 0; index < TrackResourcePaths.Length; index++)
        {
            var clip = Resources.Load<AudioClip>(TrackResourcePaths[index]);
            if (clip is null)
            {
                Debug.LogWarning($"[Music] Track not found in Resources at '{TrackResourcePaths[index]}'.");
                continue;
            }

            _playlist.Add(clip);
        }

        PlayNextTrack();
    }

    private void Update()
    {
        if (_audioSource is not null && !_audioSource.isPlaying && _playlist.Count > 0)
        {
            PlayNextTrack();
        }
    }

    private void PlayNextTrack()
    {
        if (_audioSource is null || _playlist.Count == 0)
        {
            return;
        }

        _trackIndex = (_trackIndex + 1) % _playlist.Count;
        _audioSource.clip = _playlist[_trackIndex];
        _audioSource.Play();
    }

    private static float LoadSavedVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreferenceKey, DefaultVolume));
    }
}
