using Mirror;
using UnityEngine;

/// <summary>
/// Проигрывает звук шага локально (мгновенно, без сети) и рассылает
/// остальным клиентам через сервер, чтобы они услышали шаг в 3D
/// в точке, где реально стоит игрок.
///
/// Вызывается из PlayerMovement.HandleHeadBob() в момент пересечения
/// bob-синусоидой нуля (= момент касания ногой земли).
/// </summary>
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
    [Tooltip("Коллайдеры земли с тегом Stone определяются как камень, всё остальное - Ground")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float raycastDistance = 1.2f;
    [SerializeField] private float raycastUpOffset = 0.1f;

    [Header("3D Sound (для остальных клиентов)")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 15f;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Footstep Timing")]
    [Tooltip("Минимальный интервал между шагами при ходьбе (сек) - страховка от слишком частого триггера")]
    [SerializeField] private float walkStepInterval = 0.42f;
    [Tooltip("Минимальный интервал между шагами при беге (сек)")]
    [SerializeField] private float runStepInterval = 0.28f;

    private float _lastStepTime = -999f;

    // чтобы не проигрывать один и тот же клип два раза подряд
    private int _lastStoneIndex = -1;
    private int _lastGroundIndex = -1;

    /// <summary>Вызывать только с локального игрока (PlayerMovement уже гарантирует isLocalPlayer).</summary>
    public void TriggerFootstep(bool isRunning)
    {
        if (!isLocalPlayer) return;

        float interval = isRunning ? runStepInterval : walkStepInterval;
        if (Time.time - _lastStepTime < interval) return;
        _lastStepTime = Time.time;

        Surface surface = DetectSurface();
        AudioClip clip = PickClip(surface, isRunning);
        if (clip == null) return;

        // 1) сам слышишь сразу, без задержки на сеть
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);

        // 2) остальным - через сервер, в 3D, в нашей позиции
        CmdFootstep((byte)surface, isRunning, transform.position);
    }

    private Surface DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * raycastUpOffset;

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

        // фоллбэк: если для бега нет отдельных клипов - используем ходьбу
        if (clips == null || clips.Length == 0)
        {
            clips = surface == Surface.Stone ? stoneWalkClips : groundWalkClips;
        }

        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int lastIndex = surface == Surface.Stone ? _lastStoneIndex : _lastGroundIndex;

        int index;
        do
        {
            index = Random.Range(0, clips.Length);
        } while (index == lastIndex);

        if (surface == Surface.Stone) _lastStoneIndex = index;
        else _lastGroundIndex = index;

        return clips[index];
    }

    [Command]
    private void CmdFootstep(byte surface, bool isRunning, Vector3 position)
    {
        RpcFootstep(surface, isRunning, position);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcFootstep(byte surface, bool isRunning, Vector3 position)
    {
        AudioClip clip = PickClip((Surface)surface, isRunning);
        if (clip == null) return;

        FootstepAudioPool.PlayAt(position, clip, minDistance, maxDistance, volume);
    }
}