using UnityEngine;

namespace MyAssets.scripts.Kotenkoff.General
{
    public static class Utilities
    {
        
        #region Items

        public static class Items
        {
            public static bool IsRepairTool(GameObject gameObject) => gameObject.GetComponent<IRepairTool>() != null;
        }
        
        #endregion
    }
    
    public static class Enums
    {
        /// <summary>Список приводов для транспорта.</summary>
        public enum VehicleActuator
        {
            /// <summary>Полный привод.</summary> 
            [Tooltip("Полный привод.")] Full,
        
            /// <summary>Передний привод.</summary> 
            [Tooltip("Передний привод.")] Forward,
        
            /// <summary>Задний привод.</summary> 
            [Tooltip("Задний привод.")] Backward
        }
    
        /// <summary>Список осей для поворота.</summary>
        public enum RotationDirection
        {
            X,
            Y,
            Z
        }

        /// <summary>Список направлений.</summary>
        public enum Direction
        {
            /// <summary>Направление '<b>Вперёд</b>'.</summary>
            [Tooltip("Направление 'Вперёд'.")] Forward,
            /// <summary>Направление '<b>Назад</b>'.</summary>
            [Tooltip("Направление 'Назад'.")] Backward,
            /// <summary>Направление '<b>Направо</b>'.</summary>
            [Tooltip("Направление 'Направо'.")] Rightward,
            /// <summary>Направление '<b>Налево</b>'.</summary>
            [Tooltip("Направление 'Налево'.")] Leftward
        }

        public enum AnimationType
        {
            /// <summary>Тип '<b>Только поворот</b>'.</summary>
            [Tooltip("Только поворот."), InspectorName("Только поворот")] RotateOnly,
            /// <summary>Тип '<b>Только движение</b>'.</summary>
            [Tooltip("Движение только."), InspectorName("Движение только")] MoveOnly,
            /// <summary>Тип '<b>Всё вместе</b>'.</summary>
            [Tooltip("Всё вместе."), InspectorName("Оба")] Both
        }
    }
}