using System;
using System.Collections.Generic;
using Mirror;
using MyAssets.scripts.Kotenkoff.General;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.scripts.Kotenkoff.Vehicle
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class VehicleMovement : NetworkBehaviour
    {
        [SerializeField, Tooltip("Передние моторные колёса.")] private List<GameObject> forwardMovingWheels;
        [SerializeField, Tooltip("Задние моторные колёса.")] private List<GameObject> backwardMovingWheels;
        
        
        [SerializeField, Tooltip("Поворачивающиеся рессоры.")] private List<GameObject> rotatingRessores;
        
        [SerializeField, Tooltip("Скорость вращения колёс.")] private int motorSpeed = 2000;
        [SerializeField, Tooltip("Сила вращения колёс.")] private int motorForce = 100;

        [SerializeField, Tooltip("Угол поворота колёс.")] private float rotateAngle = 35f;
        
        [SerializeField, Tooltip("Ось поворота колёс.")]
        private Enums.RotationDirection rotateDirection;
        [SerializeField, Tooltip("Привод транспорта.")]
        private Enums.VehicleActuator vehicleActuator;
        
        
        
        
        
        [ReadOnly, SerializeField, Header("(ReadOnly):")] private Vector2 moveInput;
        
        [SerializeField, ReadOnly, Tooltip("Крутятся ли сейчас колёса?")] private bool isMove;
        [SerializeField, ReadOnly, Tooltip("Опущен ли ручник?")] private bool isBreak;
        [SerializeField, ReadOnly, Tooltip("Поворачиваются ли сейчас колёса?")] private bool isRotate;

        private void OnMovement(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        private void OnBreak(InputValue value)
        {
            isBreak = !isBreak;
        }


        private void Update()
        {
            Movement();
            
            
            Breaking();
            Rotate();
        }

        // Передвижение
        private void Movement()
        {
            switch (moveInput.y)
            {
                case > 0.01f:
                {
                    isMove = true;

                    switch (vehicleActuator)
                    {
                       case  Enums.VehicleActuator.Forward:
                           ForwardActuatorMovement( Enums.Direction.Forward);
                           break;
                       case  Enums.VehicleActuator.Backward:
                           BackActuatorMovement( Enums.Direction.Forward);
                           break;
                       case  Enums.VehicleActuator.Full:
                           FuelActuatorMovement( Enums.Direction.Forward);
                           break;
                    }
                    
                    break;
                }
                case < -0.01f:
                {
                    isMove = true;

                    switch (vehicleActuator)
                    {
                        case  Enums.VehicleActuator.Forward:
                            ForwardActuatorMovement( Enums.Direction.Backward);
                            break;
                        case  Enums.VehicleActuator.Backward:
                            BackActuatorMovement( Enums.Direction.Backward);
                            break;
                        case  Enums.VehicleActuator.Full:
                            FuelActuatorMovement( Enums.Direction.Backward);
                            break;
                    }

                    break;
                }
                default:
                {
                    isMove = false;

                    // Остановка передних колёс
                    foreach (var wheel in forwardMovingWheels)
                    {
                        var o = wheel;
                        Gas(ref o, 0, 0);
                    }
                    // Остановка задних колёс
                    foreach (var wheel in backwardMovingWheels)
                    {
                        var o =  wheel;
                        Gas(ref o, 0, 0);
                    }

                    break;
                }
            }
        }

        private void ForwardActuatorMovement( Enums.Direction direction)
        {
            var speed = direction ==  Enums.Direction.Forward ? motorSpeed : -motorSpeed;
            
            foreach (var wheel in forwardMovingWheels)
            {
                var o = wheel;
                Gas(ref o, speed, motorForce);
            }
        }

        private void BackActuatorMovement( Enums.Direction direction)
        {
            var speed = direction ==  Enums.Direction.Forward ? motorSpeed : -motorSpeed;

            foreach (var wheel in backwardMovingWheels)
            {
                var o = wheel;
                Gas(ref o, speed, motorForce);
            }
        }

        private void FuelActuatorMovement( Enums.Direction direction)
        {
            var speed  = direction ==  Enums.Direction.Forward ? motorSpeed : -motorSpeed;

            foreach (var wheel in forwardMovingWheels)
            {
                var o = wheel;
                
                Gas(ref o, speed, motorForce);
            }

            foreach (var wheel in backwardMovingWheels)
            {
                var o = wheel;
                
                if (isRotate) Gas(ref o, speed / 2f, motorForce / 2f);
                else Gas(ref o, speed, motorForce);
            }
        }
        
        
        // Ручной тормоз
        private void Breaking()
        {
            if (isBreak)
            {
                foreach (var wheel in forwardMovingWheels)
                {
                    var o = wheel;
                    BreakDown(ref o);
                }

                foreach (var wheel in backwardMovingWheels)
                {
                    var o = wheel;
                    BreakDown(ref o);
                }
            }
            else
            {
                foreach (var wheel in forwardMovingWheels)
                {
                    var o = wheel;
                    BreakUp(ref o);
                }
                
                foreach (var wheel in backwardMovingWheels)
                {
                    var o = wheel;
                    BreakUp(ref o);
                }
            }
        }

        // Поворот колёс
        private void Rotate()
        {
            switch (moveInput.x)
            {
                case > 0.01f:
                {
                    foreach (var wheel in rotatingRessores)
                    {
                        var o = wheel;
                    
                        RotateWheel(ref o, -rotateAngle);
                    }

                    isRotate = true;
                    break;
                }
                case < -0.01f:
                {
                    foreach (var wheel in rotatingRessores)
                    {
                        var o = wheel;
                    
                        RotateWheel(ref o, rotateAngle);
                    }

                    isRotate = true;
                    break;
                }
                default:
                {
                    foreach (var ressore in rotatingRessores)
                    {
                        var o = ressore;
                    
                        RotateWheel(ref o, 0);
                    }

                    isRotate = false;
                    break;
                }
            }
        }

        
        
        private void Gas(ref GameObject wheel, float speed, float force)
        {
            if (wheel == null) return;
            
            var motor = wheel.GetComponent<HingeJoint>().motor;

            if (!wheel.GetComponent<HingeJoint>().useMotor) return;
            
            motor.targetVelocity = speed;
            motor.force = force;
            
            wheel.GetComponent<HingeJoint>().motor = motor;
        }
        
        private void BreakDown(ref GameObject wheel)
        {
            if (wheel == null) return;
            
            var joint = wheel.GetComponent<HingeJoint>();

            joint.useMotor = false;
            joint.useSpring = true;

            var spring = joint.spring;
            spring.spring = 1000;
            
            joint.spring = spring;
        }

        private void BreakUp(ref GameObject wheel)
        {
            if (wheel == null) return;
            
            var joint = wheel.GetComponent<HingeJoint>();
            
            joint.useSpring = false;
            joint.useMotor = true;
        }

        private void RotateWheel(ref GameObject wheel, float angle)
        {
            if (wheel == null) return;
            
            var joint =  wheel.GetComponent<ConfigurableJoint>();

            var rot = rotateDirection switch
            {
                 Enums.RotationDirection.X => Quaternion.Euler(angle, 0, 0),
                 Enums.RotationDirection.Y => Quaternion.Euler(0, angle, 0),
                 Enums.RotationDirection.Z => Quaternion.Euler(0, 0, angle),
                _ => Quaternion.Euler(0, 0, 0)
            };

            joint.targetRotation = rot;
        }
    }
}