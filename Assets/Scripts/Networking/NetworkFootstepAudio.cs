using Mirror;
using UnityEngine;

// ============================================================
// NetworkFootstepAudio
// Plays local footstep audio instantly and syncs 3D spatial sounds to remote clients.
// Проигрывает звук шага локально (без задержки) и синхронизирует 3D-звуки для остальных игроков.
// Triggered by PlayerMovement head-bobbing zero-crossings (foot touching ground).
// Вызывается из PlayerMovement при пересечении качанием головы нуля (касание земли).
// ============================================================
public class NetworkFootstepAudio : NetworkBehaviour
{
    private enum Surface : byte
    {
        Ground = 0,
        Stone = 1,
    }

    [Header("Clips - Stone")]
    [SerializeField] private AudioClip[] stoneWalkClips;
    [SerializeField] private AudioClip[] stoneRunClips;

    [Header("Clips - Ground")]
    [SerializeField] private AudioClip[] groundWalkClips;
    [SerializeField] private AudioClip[] groundRunClips;

    [Header("Surface Detection")]
    [Tooltip("Ground colliders tagged 'Stone' detect as Stone surface; all others default to Ground.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastDistance = 1.2f;
    [SerializeField] private float raycastUpOffset = 0.1f;

    [Header("3D Sound (For Remote Clients)")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 15f;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Footstep Timing")]
    [Tooltip("Minimum delay between walking steps (seconds) to prevent trigger spam.")]
    // Минимальный интервал между шагами при ходьбе (сек) — страховка от спама
    [SerializeField] private float walkStepInterval = 0.42f;
    
    [Tooltip("Minimum delay between running steps (seconds).")]
    // Минимальный интервал между шагами при беге (сек)
    [SerializeField] private float runStepInterval = 0.28f;

    private float _lastStepTime = -999f;

    // Cache indices to avoid repeating the same audio clip twice in a row
    // Кэш индексов, чтобы избегать повтора одного и того же клипа дважды подряд
    private int _lastStoneIndex = -1;
    private int _lastGroundIndex = -1;

    // Call only on the local player authority (PlayerMovement guarantees isLocalPlayer check)
    // Вызывать только на локальном игроке (PlayerMovement гарантирует проверку isLocalPlayer)
    public void TriggerFootstep(bool isRunning)
    {
        if (!isLocalPlayer) return;

        float interval = isRunning ? runStepInterval : walkStepInterval;
        if (Time.time - _lastStepTime < interval) return;
        _lastStepTime = Time.time;

        Surface surface = DetectSurface();
        AudioClip clip = PickClip(surface, isRunning);
        if (clip == null) return;

        // 1. Play local sound instantly with zero network latency
        // 1. Слышим свой шаг мгновенно, без задержек сети
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);

        // 2. Broadcast step sound through server to other clients at our exact position
        // 2. Рассылаем шаг через сервер остальным игрокам в нашей текущей позиции
        CmdFootstep((byte)surface, isRunning, transform.position);
    }

    private Surface DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * raycastUpOffset;

        // Raycast downward to inspect surface tag directly beneath the player
        // Рейкаст вниз для определения тега поверхности под ногами игрока
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            if (hit.collider.CompareTag("Stone"))
                return Surface.Stone;
        }

        return Surface.Ground;
    }

    private AudioClip PickClip(Surface surface, bool isRunning)
    {
        AudioClip[] clips = surface switch
        {
            Surface.Stone => isRunning ? stoneRunClips : stoneWalkClips,
            _ => isRunning ? groundRunClips : groundWalkClips,
        };

        // Fallback: Use walking clips if running array is empty or unassigned
        // Фоллбэк: если клипы для бега не заданы, используем клипы ходьбы
        if (clips == null || clips.Length == 0)
        {
            clips = surface == Surface.Stone ? stoneWalkClips : groundWalkClips;
        }

        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int lastIndex = surface == Surface.Stone ? _lastStoneIndex : _lastGroundIndex;

        // Pick a non-repeating random clip
        // Выбираем случайный клип без повтора предыдущего
        int index;
        do
        {
            index = Random.Range(0, clips.Length);
        } while (index == lastIndex);

        if (surface == Surface.Stone) _lastStoneIndex = index;
        else _lastGroundIndex = index;

        return clips[index];
    }

    // Command sent from local owner client to server
    // Команда отправляется с локального клиента-владельца на сервер
    [Command]
    private void CmdFootstep(byte surface, bool isRunning, Vector3 position)
    {
        RpcFootstep(surface, isRunning, position);
    }

    // ClientRpc excluding owner to prevent duplicate audio playback on origin client
    // ClientRpc с пропуском владельца (includeOwner = false), чтобы исключить дублирование звука
    [ClientRpc(includeOwner = false)]
    private void RpcFootstep(byte surface, bool isRunning, Vector3 position)
    {
        AudioClip clip = PickClip((Surface)surface, isRunning);
        if (clip == null) return;

        // Route spatial audio playback through external pool to eliminate GC allocations
        // Воспроизводим 3D-звук через пул объектов, исключая лишние аллокации GC
        FootstepAudioPool.PlayAt(position, clip, minDistance, maxDistance, volume);
    }
}