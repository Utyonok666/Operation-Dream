using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// FootstepAudioPool
// Static pool of reusable AudioSources for footstep sounds.
// Статический пул переиспользуемых AudioSource для звуков шагов.
// Avoids Instantiate/Destroy spam in multiplayer (GC-friendly).
// Позволяет не спамить Instantiate/Destroy в мультиплеере (щадит GC).
// ============================================================
public static class FootstepAudioPool
{
    private class PoolRunner : MonoBehaviour { } // Dummy host for coroutines // Пустышка-хост для корутин

    private static readonly Queue<AudioSource> _pool = new(); // Free sources // Свободные источники
    private static PoolRunner _runner;
    private static Transform _root; // Parent for tidy hierarchy // Родитель для порядка в иерархии

    // Main entry point: play a 3D sound at position // Главный метод: проиграть 3D-звук в точке
    public static void PlayAt(Vector3 position, AudioClip clip, float minDistance, float maxDistance, float volume)
    {
        EnsureInitialized(); // Lazy init on first call // Ленивая инициализация при первом вызове

        // Reuse a free source, or create a new one if pool is empty
        // Берём свободный источник, либо создаём новый если пул пуст
        AudioSource src = _pool.Count > 0 ? _pool.Dequeue() : CreateSource();

        src.transform.position = position;
        src.clip = clip;
        src.minDistance = minDistance; // Full volume within this range // Полная громкость в этом радиусе
        src.maxDistance = maxDistance; // Inaudible beyond this range // Не слышно дальше этого радиуса
        src.volume = volume;
        src.Play();

        _runner.StartCoroutine(ReturnAfterDelay(src, clip.length)); // Return to pool when done // Вернуть в пул по окончании
    }

    private static void EnsureInitialized()
    {
        if (_runner != null) return; // Already set up // Уже инициализировано

        var go = new GameObject("~FootstepAudioPool"); // "~" = internal/system object // "~" = служебный объект
        Object.DontDestroyOnLoad(go); // Survive scene changes // Переживает смену сцены
        _root = go.transform;
        _runner = go.AddComponent<PoolRunner>();
    }

    // Creates a fresh 3D positional audio source // Создаёт новый источник для 3D-звука
    private static AudioSource CreateSource()
    {
        var go = new GameObject("PooledFootstepSource");
        go.transform.SetParent(_root);

        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;              // Fully 3D sound // Полностью 3D звук
        src.rolloffMode = AudioRolloffMode.Linear; // Linear falloff by distance // Линейное затухание по дистанции
        src.playOnAwake = false;

        return src;
    }

    // Waits for clip duration, then returns source to pool
    // Ждёт длительность клипа, потом возвращает источник в пул
    private static IEnumerator ReturnAfterDelay(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        _pool.Enqueue(src);
    }
}