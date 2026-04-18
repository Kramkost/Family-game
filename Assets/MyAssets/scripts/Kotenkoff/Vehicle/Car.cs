using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.scripts.Kotenkoff.Vehicle
{
    public class Car :  MonoBehaviour
    {
        [SerializeField, Tooltip("Передние моторные колёса.")] private List<GameObject> forwardMovingWheels;
        [SerializeField, Tooltip("Задние моторные колёса.")] private List<GameObject> backMovingWheels;
        
        
        [SerializeField, Tooltip("Поворачивающиеся рессоры.")] private List<GameObject> rotatingRessores;
        
        public int motorSpeed = 2000;
        public int motorForce = 100;
        
        public float rotateAngle = 35f;
        
        [SerializeField, Tooltip("Ось поворота колёс.")]
        private RotationDirection rotateDirection;
        
        
        
        [ReadOnly, Space(5)] public Vector2 moveInput;
        
        [SerializeField, ReadOnly] private bool isMoving;
        [SerializeField, ReadOnly] private bool isBreak;
        [SerializeField, ReadOnly] private bool isRotate;

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

        private void Movement()
        {
            switch (moveInput.y)
            {
                case > 0.01f:
                {
                    isMoving = true;

                    foreach (var wheel in forwardMovingWheels)
                    {
                        var o = wheel;
                        Gas(ref o, motorSpeed, motorForce);
                    }

                    foreach (var wheel in backMovingWheels)
                    {
                        var o = wheel;
                        var speed = isRotate ? motorSpeed / 2 : motorSpeed;
                        var force = isRotate ? motorForce / 2 : motorForce;
                        
                        Gas(ref o, speed, force);
                    }

                    break;
                }
                case < -0.01f:
                {
                    isMoving = true;

                    foreach (var wheel in forwardMovingWheels)
                    {
                        var o = wheel;
                        Gas(ref o, -motorSpeed, motorForce);
                    }
                    
                    foreach (var wheel in backMovingWheels)
                    {
                        var o = wheel;
                        var speed = isRotate ? 0 : motorSpeed;
                        var force = isRotate ? 0 : motorForce;
                        
                        Gas(ref o, -speed, force);
                    }

                    break;
                }
                default:
                {
                    isMoving = false;

                    foreach (var wheel in forwardMovingWheels)
                    {
                        var o = wheel;
                        Gas(ref o, 0, 0);
                    }

                    foreach (var wheel in backMovingWheels)
                    {
                        var o =  wheel;
                        Gas(ref o, 0, 0);
                    }

                    break;
                }
            }
        }

        private void Breaking()
        {
            if (isBreak)
            {
                foreach (var wheel in forwardMovingWheels)
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
            }
        }

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

            motor.targetVelocity = speed;
            motor.force = force;
            
            wheel.GetComponent<HingeJoint>().motor = motor;
        }
        
        private void BreakDown(ref GameObject wheel)
        {
            var joint = wheel.GetComponent<HingeJoint>();

            joint.useMotor = false;
            joint.useSpring = true;

            var spring = joint.spring;
            spring.spring = 1000;
            
            joint.spring = spring;
        }

        private void BreakUp(ref GameObject wheel)
        {
            var joint = wheel.GetComponent<HingeJoint>();
            
            joint.useSpring = false;
            joint.useMotor = true;
        }

        private void RotateWheel(ref GameObject wheel, float angle)
        {
            var joint =  wheel.GetComponent<ConfigurableJoint>();

            var rot = rotateDirection switch
            {
                RotationDirection.X => Quaternion.Euler(angle, 0, 0),
                RotationDirection.Y => Quaternion.Euler(0, angle, 0),
                RotationDirection.Z => Quaternion.Euler(0, 0, angle),
                _ => Quaternion.Euler(0, 0, 0)
            };

            joint.targetRotation = rot;
        }
    }
    
    internal enum RotationDirection
    {
        X,
        Y,
        Z
    }
}