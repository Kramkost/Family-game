using Mirror;
using Unity.Cinemachine;
using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.Character
{
    [RequireComponent(typeof(CharacterBase))]
    public class CharacterCam : NetworkBehaviour
    {
        [SerializeField] CharacterBase characterBase;
        [SerializeField] private Camera cam;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        
        public void Start()
        {
            if (cinemachineCamera == null) cinemachineCamera = gameObject.GetComponent<CinemachineCamera>();
            
            if (isLocalPlayer)
            {
                cam.gameObject.SetActive(true);
                cinemachineCamera.gameObject.SetActive(true);
                characterBase.FpCamera = cinemachineCamera;
            }
            else
            {
                cam.gameObject.SetActive(false);
                cinemachineCamera.gameObject.SetActive(false);
            }
        }
    }
}
