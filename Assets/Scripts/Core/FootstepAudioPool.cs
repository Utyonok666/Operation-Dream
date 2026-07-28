using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Статический пул 3D AudioSource-ов. Нужен, чтобы не плодить
/// GameObject.Instantiate/Destroy на каждый шаг чужого игрока -
/// это дёшево, но 30+ игроков * несколько шагов в секунду это заметно нагадит в GC.
/// </summary>
public static class FootstepAudioPool
{
    private class PoolRunner : MonoBehaviour { }

    private static readonly Queue<AudioSource> _pool = new();
    private static PoolRunner _runner;
    private static Transform _root;

    public static void PlayAt(Vector3 position, AudioClip clip, float minDistance, float maxDistance, float volume)
    {
        EnsureInitialized();

        AudioSource src = _pool.Count > 0 ? _pool.Dequeue() : CreateSource();

        src.transform.position = position;
        src.clip = clip;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.volume = volume;
        src.Play();

        _runner.StartCoroutine(ReturnAfterDelay(src, clip.length));
    }

    private static void EnsureInitialized()
    {
        if (_runner != null) return;

        var go = new GameObject("~FootstepAudioPool");
        Object.DontDestroyOnLoad(go);
        _root = go.transform;
        _runner = go.AddComponent<PoolRunner>();
    }

    private static AudioSource CreateSource()
    {
        var go = new GameObject("PooledFootstepSource");
        go.transform.SetParent(_root);

        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;              // полностью 3D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.playOnAwake = false;

        return src;
    }

    private static IEnumerator ReturnAfterDelay(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        _pool.Enqueue(src);
    }
}
