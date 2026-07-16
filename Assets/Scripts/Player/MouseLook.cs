using UnityEngine;
using Mirror;

public class MouseLook : NetworkBehaviour
{
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private Transform playerBody; // Ссылка на сам объект Player

    [SerializeField] private NetworkLook networkLook;

    private float _xRotation = 0f;

    // Mirror вызывает это ТОЛЬКО на объекте, которым управляет именно этот клиент
    public override void OnStartLocalPlayer()
    {
        // Курсор блокируем только у своего игрока — иначе он будет прыгать в центр
        // экрана у каждого клиента при заходе ЛЮБОГО игрока на сервер, включая чужих
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Rotate(Vector2 lookInput)
    {
        // Подстраховка: Rotate() и так вызывается только из инпута локального игрока
        // (см. PlayerMovement), но если когда-нибудь дёрнешь его откуда-то ещё — не сломает чужих
        if (!isLocalPlayer)
            return;

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // Вращаем камеру вверх-вниз (ось X)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f); // Ограничиваем, чтобы не сломать шею
        
        if (networkLook != null)
            networkLook.SetLook(_xRotation);

        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Вращаем тело игрока влево-вправо (ось Y)
        playerBody.Rotate(Vector3.up * mouseX);
    }
}