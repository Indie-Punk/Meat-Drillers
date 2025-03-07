﻿using System;
using System.Collections.Generic;
using _CODE._NETCODE;
using _CODE.Interactable;
using ECM2;
using ECM2.Examples.FirstPerson;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace _CODE
{
    public class InteractionController : NetworkBehaviour
    {
        [SerializeField] private FirstPersonCharacter character;
        [SerializeField] private FirstPersonLook look;
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private KickController kickController;
        [SerializeField] private Transform raycastPos;
        [SerializeField] public BurpController burpController;
        [SerializeField] private LayerMask interactionLayerMask;

        [SerializeField] public Transform takePos;

        // [SerializeField] private Beer beer;
        [SerializeField] private InteractableItem interactItem;
        [SerializeField] private float pushForce;
        [SerializeField] private float torqueForce;
        public bool isSit;
        public bool IsSit => isSit;

        [Header("Take and Drop logic")] [SerializeField]
        GameObject meatPrefab;

        public SittingPlace sitPlace;

        private void Update()
        {
            if (!IsOwner)
                return;
            Kick();
            if (interactItem != null)
            {
                interactItem.transform.position = takePos.position;
                interactItem.transform.rotation = takePos.rotation;
            }

            if (Input.GetKeyDown(KeyCode.M))
                SpawnMeatRpc();
            Interaction();
            if (interactItem == null)
            {
                Searching();
            }
        }

        void Kick()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                PushableObject pushableObject = null;
                if (hit.collider != null)
                    pushableObject = hit.collider.GetComponent<PushableObject>();
                kickController.Kick(pushableObject);
            }
        }

        [Rpc(SendTo.Server)]
        void SpawnMeatRpc()
        {
            var meat = Instantiate(meatPrefab, takePos.position, Quaternion.identity);
            meat.GetComponent<NetworkObject>().Spawn(true);
        }

        [Rpc(SendTo.Everyone)]
        private void TakeRpc(NetworkBehaviourReference nb)
        {
            if (nb.TryGet<InteractableItem>(out var item))
            {
                interactItem = item;
                interactItem.Take();
                // TakeRpc(item);
                interactItem.GetComponent<ClientNetworkTransform>().enabled = false;
                interactItem.GetComponent<Collider>().enabled = false;
                interactItem.GetComponent<Rigidbody>().isKinematic = true;
                interactItem.transform.localPosition = Vector3.zero;
                interactItem.OnDestroy += () => { interactItem = null; };
            }
        }

        [Rpc(SendTo.Everyone)]
        private void DropRpc()
        {
            interactItem.transform.position = takePos.position;
            interactItem.transform.rotation = takePos.rotation;
            interactItem.GetComponent<Collider>().enabled = true;
            interactItem.GetComponent<ClientNetworkTransform>().enabled = true;
            var rbItem = interactItem.GetComponent<Rigidbody>();
            rbItem.isKinematic = false;
            rbItem.AddForce(takePos.forward * pushForce, ForceMode.Impulse);
            rbItem.AddTorque(takePos.up * torqueForce, ForceMode.Impulse);
            interactItem = null;
        }

        private void Interaction()
        {
            if (interactItem != null)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("Use");
                    interactItem.Use(this);
                    // interactItem = null;
                }
                else if (Input.GetKeyDown(KeyCode.G))
                {
                    DropRpc();
                }
            }
        }

        public void StandUp()
        {
            if (!isSit)
                return;
            isSit = false;
            sitPlace.SeatControlRpc(false);
            SetLookAngles(new Vector4(-80,80,-180,180));
            SetPosRpc(sitPlace.StandUpPos, false);
            sitPlace = null;
        }

        void SetLookAngles(Vector4 angles)
        {
            look.minPitch = angles.x; 
            look.maxPitch = angles.y;
            look.minYaw = angles.z;
            look.maxYaw = angles.w;
        }

        void SitDown()
        {
            if (sitPlace.IsBusy)
                return;
            
            isSit = true;
            sitPlace.SeatControlRpc(true);
            SetLookAngles(sitPlace.AngleLock);
            SetPosRpc(sitPlace.SitPos, true);
            
        }

        [Rpc(SendTo.Everyone)]
        void SetPosRpc(Vector3 pos, bool pause)
        {
            character.Pause(pause);
            characterMovement.SetPositionAndRotation(pos, 
                Quaternion.FromToRotation(sitPlace.StartAngle, sitPlace.transform.up));
            character.SetDefaultCameraValues();
            // character.SetCameraYaw(0);
        }

        RaycastHit hit;

        private void Searching()
        {
            // Does the ray intersect any objects excluding the player layer
            if (Physics.Raycast(raycastPos.position, raycastPos.forward, out hit, 10, interactionLayerMask))
            {
                if (hit.transform.gameObject.TryGetComponent<SittingPlace>(out var sit))
                {
                    sitPlace = sit;
                    if (Input.GetKeyDown(KeyCode.E))
                        SitDown();
                }
                else if (!isSit)
                    sitPlace = null;

                if (hit.transform.gameObject.TryGetComponent<PushBtn>(out var btn))
                {
                    if (Input.GetKeyDown(KeyCode.E))
                        btn.TouchServerRpc();
                }
                else if (hit.transform.gameObject.TryGetComponent<InteractableItem>(out var item))
                {
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        Debug.Log("Take");

                        TakeRpc(item);
                    }
                }

                Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance,
                    Color.yellow);
                // Debug.Log("Did Hit"); 
            }
            else
            {
                Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);
                // Debug.Log("Did not Hit"); 
            }
        }
    }
}