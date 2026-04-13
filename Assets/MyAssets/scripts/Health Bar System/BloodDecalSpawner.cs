using Mirror;
using UnityEngine;

namespace Health_Bar_System
{
    public class BloodDecalSpawner : NetworkBehaviour
    {
        [Header("Настройки Крови")]
        [Tooltip("Префаб лужи крови (Quad с материалом)")]
        [SerializeField] private GameObject bloodDecalPrefab;
        
        [Tooltip("Слои, на которых может оставаться кровь (Земля, Пол, Дорога)")]
        [SerializeField] private LayerMask groundLayer;
        
        [Tooltip("Случайный размер лужи (Мин, Макс)")]
        [SerializeField] private Vector2 decalScaleRange = new Vector2(0.8f, 1.5f);

        /// <summary>
        /// Вызывать на сервере при смерти игрока.
        /// </summary>
        [Server]
        public void SpawnBloodAt(Vector3 deathPosition)
        {
            
            RpcSpawnBloodVisual(deathPosition);
        }

        [ClientRpc]
        private void RpcSpawnBloodVisual(Vector3 position)
        {
            if (bloodDecalPrefab == null) return;

           
            Vector3 rayStart = position + Vector3.up * 0.5f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, groundLayer))
            {
                // 1. Позиция: точка попадания луча + смещение на 1-2 миллиметра вверх по нормали поверхности
                // (чтобы текстура крови не проваливалась в текстуру асфальта)
                Vector3 spawnPos = hit.point + hit.normal * 0.02f;

                // 2. Вращение: выравниваем декаль по нормали поверхности (чтобы она легла ровно на склон)
                Quaternion spawnRot = Quaternion.LookRotation(hit.normal);

                // Спавним локально
                GameObject bloodInstance = Instantiate(bloodDecalPrefab, spawnPos, spawnRot);

                // 3. Поворачиваем кровь случайно по оси Z, чтобы лужи не выглядели одинаково
                bloodInstance.transform.Rotate(Vector3.forward, Random.Range(0f, 360f));

                // 4. Задаем случайный размер
                float randomScale = Random.Range(decalScaleRange.x, decalScaleRange.y);
                bloodInstance.transform.localScale = new Vector3(randomScale, randomScale, 1f);

                // 5. Опционально: удаляем кровь через 30-60 секунд, чтобы не забивать память
                Destroy(bloodInstance, 60f);
            }
        }
    }
}