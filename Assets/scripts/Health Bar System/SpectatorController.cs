using UnityEngine;
using UnityEngine.InputSystem;

namespace Health_Bar_System
{
    public class SpectatorController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float flySpeed = 10f;
        [SerializeField] private float lookSensitivity = 0.2f;
        
        [Header("References")]
        [SerializeField] private Camera mainPlayerCamera;
        [SerializeField] private Camera spectatorCamera; 

        private bool isSpectating = false;
        private float pitch = 0f;
        private float yaw = 0f;

        private void Start()
        {

            if (spectatorCamera != null) spectatorCamera.gameObject.SetActive(false);
        }

        public void EnableSpectator()
        {
            isSpectating = true;
         
            if (mainPlayerCamera != null) mainPlayerCamera.gameObject.SetActive(false);
            if (spectatorCamera != null)
            {
                spectatorCamera.gameObject.SetActive(true);
        
                spectatorCamera.transform.SetParent(null); 
            }
        }

        public void DisableSpectator()
        {
            isSpectating = false;
            if (spectatorCamera != null)
            {
                spectatorCamera.gameObject.SetActive(false);
            
                spectatorCamera.transform.SetParent(transform); 
            }
            if (mainPlayerCamera != null) mainPlayerCamera.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!isSpectating || spectatorCamera == null) return;

            HandleLook();
            HandleFlight();
        }

        private void HandleLook()
        {
            
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * lookSensitivity;
            
            yaw += mouseDelta.x;
            pitch -= mouseDelta.y;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            spectatorCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleFlight()
        {
            Vector3 moveDir = Vector3.zero;

            if (Keyboard.current.wKey.isPressed) moveDir += spectatorCamera.transform.forward;
            if (Keyboard.current.sKey.isPressed) moveDir -= spectatorCamera.transform.forward;
            if (Keyboard.current.aKey.isPressed) moveDir -= spectatorCamera.transform.right;
            if (Keyboard.current.dKey.isPressed) moveDir += spectatorCamera.transform.right;
            
            if (Keyboard.current.spaceKey.isPressed) moveDir += Vector3.up;
            if (Keyboard.current.ctrlKey.isPressed) moveDir -= Vector3.up;

            spectatorCamera.transform.position += moveDir.normalized * flySpeed * Time.deltaTime;
        }
    }
}