using System;
using System.Linq;
using Mirror;
using MyAssets.scripts.Kotenkoff.Character.Interfaces;
using MyAssets.scripts.Kotenkoff.Character.Inventory;
using MyAssets.scripts.Kotenkoff.Items;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.scripts.Kotenkoff.Character
{
    /// <summary>
    /// Класс для осуществления взаимодействия.
    /// </summary>
    public class CharacterInteract : NetworkBehaviour
    {
        [SerializeField, Tooltip("Дистанция взаимодействия."), Header("Взаимодействие:")]
        private float interactionDistance;
        [SerializeField, Tooltip("Слои, с которыми может взаимодействовать Raycast.")]
        private LayerMask raycastLayerMasks;

        [SerializeField, Tooltip("Включает raycast луч для отладки.")]
        private bool drawRayForDebug;
        
        
        [Header("Компоненты:")]
        [SerializeField] private CharacterInventory characterInventory;
        [SerializeField] private CharacterBase characterBase;



        [SyncVar] private Ray ray;
        private RaycastHit hit;

        public override void OnStartLocalPlayer()
        {
            if (!isLocalPlayer) return;
            
            if (characterBase == null) characterBase = gameObject.GetComponent<CharacterBase>();
            if (characterInventory == null) characterInventory = gameObject.GetComponent<CharacterInventory>();
        }

        /// <summary>
        /// Основной метод для инициирования взаимодействия. Вызывается с клиента при попытке взаимодействия (например, по нажатию клавиши). <br/>
        /// Проверяет, что игрок локальный, обновляет ссылку на камеру и запускает клиентский Raycast.
        /// </summary>
        public void TryInteract()
        {
            if (!isLocalPlayer) return;
            
            if (characterBase.FpCamera == null)
            {
                TargetMessage("Ошибка: не удалось получить камеру для взаимодействия.");
                return;
            }

            if (ClientTryInteract(out GameObject hitObject))
            {
                if (isOwned)
                {
                    var rootIdentity = hitObject.GetComponentInParent<NetworkIdentity>();
                    var interactable =  hitObject.GetComponentInParent<IInteractableTest>();

                    if (interactable != null && rootIdentity != null)
                    {
                        CmdTryInteractOnServer(hitObject.GetComponentInParent<NetworkIdentity>(), ((Component)interactable).gameObject.name);
                    }
                }
            }
            else
            {
                TargetMessage("Нет объекта для взаимодействия. Возможно он не на слое взаимодействия.");
            }
        }
        
        [Command]
        private void CmdTryInteractOnServer(NetworkIdentity rootIdentity, string targetName)
        {
            // Валидация инвентаря
            if (characterInventory == null)
            {
                TargetMessage("Ошибка: инвентарь не инициализирован!");
                return;
            }

            // Валидация объекта
            if (rootIdentity == null)
            {
                TargetMessage("Ошибка: целевой объект не существует.");
                return;
            }

            var distanceToObject = Vector3.Distance(transform.position, rootIdentity.transform.position);
            if (distanceToObject > interactionDistance)
            {
                TargetMessage($"Ошибка: объект слишком далеко ({distanceToObject:F2}м > {interactionDistance}м)");
                return;
            }
            
            CmdInteract(rootIdentity, targetName);
        }
        
        private void CmdInteract(NetworkIdentity rootIdentity, string targetName)
        {
            if (rootIdentity == null || string.IsNullOrEmpty(targetName)) return;

            foreach (Transform child in rootIdentity.GetComponentsInChildren<Transform>())
            {
                if (child.name != targetName ||
                    !child.TryGetComponent(out IInteractableTest interactableTest)) continue;
                interactableTest.TryInteract(characterInventory);
                return;
            }
        }
        
        /// <summary>
        /// Выполняет Raycast на стороне клиента из позиции и направления активной камеры.
        /// Если луч попадает в объект с нужным LayerMask, возвращает этот объект. <br/>
        /// При включённом drawRayForDebug визуализирует луч для отладки.
        /// </summary>
        /// <param name="hitObject">Найденный объект взаимодействия (если есть)</param>
        /// <returns>True, если Raycast нашёл объект; false в противном случае</returns>
        private bool ClientTryInteract(out GameObject hitObject)
        {
            hitObject = null;

            Ray localRay = new Ray(characterBase.FpCamera.transform.position, characterBase.FpCamera.transform.forward);

            // Визуализация луча для отладки, если включено в инспекторе
            if (drawRayForDebug)
            {
                Debug.DrawRay(localRay.origin, localRay.direction * interactionDistance, Color.red, 1f);
            }

            if (Physics.Raycast(localRay, out hit, interactionDistance, raycastLayerMasks))
            {
                hitObject = hit.collider.gameObject;
                TargetMessage($"Клиент {netId} нашёл объект: '{hitObject.name}' на расстоянии {hit.distance:F2}м");
                return true;
            }

            return false;
        }
        
        [TargetRpc]
        private void TargetMessage(string message) => Debug.Log($"[CharacterInteract] ServerMessage: {message}.");
    }
}