using UnityEngine;
using MFPS.InputManager;
using FC_ParkourSystem;
using Photon.Pun;
using System.Collections;

namespace FC_ParkourSystem
{
    public class MFPS_IntegrationHelper : MonoBehaviour, IParkourCharacter
    {
        private ParkourController parkourController;
        private ClimbController climbController;
        private bl_FirstPersonController firstPersonController;
        private bl_PlayerAnimationsBase playerAnimations;
        private bl_PlayerRagdollBase playerRagdoll;
        private Collider playerCollider;
        private bl_PlayerCameraSwitcher cameraSwitcher;
        private bl_PlayerNetwork playerNetwork;
        private bl_PlayerReferences playerReferences;
        private Camera playerCamera;
        private PlayerState lastBodyState;

        public bool UseRootMotion { get; set; } = false;

        public Vector3 MoveDir => new Vector3(bl_GameInput.Horizontal, 0, bl_GameInput.Vertical).normalized;

        public bool IsGrounded => firstPersonController.isGrounded;

        public float Gravity => -9.81f;

        public Animator Animator
        {
            get => playerReferences.PlayerAnimator;
            set => playerReferences.PlayerAnimator = value;
        }

        public bool PreventParkourAction => firstPersonController.State == PlayerState.Crouching || firstPersonController.State == PlayerState.Running;

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Debug.Log("Initializing components in MFPS_IntegrationHelper");

            playerReferences = GetComponent<bl_PlayerReferences>();
            if (playerReferences == null)
            {
                Debug.LogError("PlayerReferences is missing.");
                this.enabled = false;
                return;
            }

            parkourController = GetComponent<ParkourController>();
            climbController = GetComponent<ClimbController>();
            firstPersonController = playerReferences.firstPersonController as bl_FirstPersonController;
            playerAnimations = playerReferences.playerAnimations;
            playerRagdoll = playerReferences.playerRagdoll;
            playerCollider = playerReferences.characterController;
            cameraSwitcher = GetComponent<bl_PlayerCameraSwitcher>();
            playerNetwork = playerReferences.playerNetwork;
            playerCamera = playerReferences.playerCamera;

            if (AreRequiredComponentsMissing())
            {
                Debug.LogError("Missing required components for MFPS_IntegrationHelper.");
                this.enabled = false;
                return;
            }

            InitializeAnimatorHelper();
        }

        private bool AreRequiredComponentsMissing()
        {
            return parkourController == null || climbController == null || firstPersonController == null ||
                   playerAnimations == null || playerRagdoll == null || playerCollider == null ||
                   cameraSwitcher == null || playerNetwork == null || playerCamera == null;
        }

        private void InitializeAnimatorHelper()
        {
            // Implementation for initializing animator helper if needed
        }

        private void Update()
        {
            HandleInputs();
            if (parkourController != null && !parkourController.ControlledByParkour)
            {
                firstPersonController.OnUpdate();
            }
            UpdateCamera();
        }

        private void FixedUpdate()
        {
            if (parkourController != null && parkourController.ControlledByParkour)
            {
                firstPersonController.mouseLook.Update();
                firstPersonController.mouseLook.UpdateLook(firstPersonController.transform, firstPersonController.headRoot);
            }
            else if (firstPersonController != null)
            {
                firstPersonController.FixedUpdate();
            }
        }

        private void HandleInputs()
        {
            float horizontal = bl_Input.HorizontalAxis;
            float vertical = bl_Input.VerticalAxis;
            bool jump = bl_GameInput.Jump();
            bool crouch = bl_GameInput.Crouch();
            bool drop = bl_GameInput.Interact(); // Assuming Interact is mapped to Drop
            bool sprint = bl_GameInput.Run();

            Vector3 move = new Vector3(horizontal, 0, vertical);
            Animator.SetFloat("Speed", move.magnitude);

            if (move != Vector3.zero)
            {
                MoveCharacter(horizontal, vertical, sprint);
            }

            if (jump)
            {
                HandleJump();
            }

            if (crouch)
            {
                HandleCrouch();
            }

            if (drop)
            {
                HandleDrop();
            }

            // Update the BodyState parameter based on the player's current state
            UpdateBodyState();
        }

        private void MoveCharacter(float horizontal, float vertical, bool sprint)
        {
            if (firstPersonController != null)
            {
                firstPersonController.MovementInput();
                if (sprint)
                {
                    firstPersonController.State = PlayerState.Running;
                }
                else
                {
                    firstPersonController.State = PlayerState.Walking;
                }
            }
        }

        private void HandleJump()
        {
            if (firstPersonController != null && firstPersonController.isGrounded && parkourController != null)
            {
                Animator.SetTrigger("Jump");
                parkourController.VerticalJump();
                firstPersonController.State = PlayerState.Jumping; // Update state to Jumping
            }
        }

        private void HandleCrouch()
        {
            if (firstPersonController != null)
            {
                if (firstPersonController.Crounching)
                {
                    firstPersonController.State = PlayerState.Crouching;
                }
                else
                {
                    firstPersonController.State = PlayerState.Walking;
                }
            }
        }

        private void HandleDrop()
        {
            if (climbController != null && !parkourController.InAction)
            {
                if (bl_Input.isButtonDown("Drop"))
                {
                    if (climbController.envScanner.DropLedgeCheck(transform.forward, out ClimbLedgeData ledgeData))
                    {
                        var currentLedge = ledgeData.ledgeHit.transform;
                        var newPoint = climbController.GetNearestPoint(currentLedge, ledgeData.ledgeHit.point, false, obstacleCheck: false);

                        if (newPoint == null) return;

                        var currentPoint = newPoint;

                        OnStartParkourAction();

                        if (climbController.CheckWall(currentPoint).Value.isWall)
                        {
                            Animator.SetFloat("freeHang", 0);
                            StartCoroutine(climbController.JumpToLedge(currentPoint.transform, "DropToHang", 0.50f, 0.90f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                            firstPersonController.State = PlayerState.Climbing; // Update state to Climbing
                        }
                        else
                        {
                            Animator.SetFloat("freeHang", 1);
                            StartCoroutine(climbController.JumpToLedge(currentPoint.transform, "DropToFreeHang", 0.50f, 0.89f, rotateToLedge: true, matchStart: AvatarTarget.LeftFoot));
                            firstPersonController.State = PlayerState.Climbing; // Update state to Climbing
                        }
                    }
                }
            }
        }

        private void UpdateCamera()
        {
            // Implement camera update logic if needed
        }

        public void OnEndParkourAction()
        {
            if (firstPersonController != null) firstPersonController.enabled = true;
            if (playerCollider != null) playerCollider.enabled = true;
            if (cameraSwitcher != null) cameraSwitcher.enabled = true;
            if (playerRagdoll != null)
            {
                playerRagdoll.SetActiveRagdollPhysics(false);
            }
            // Re-enable upper body layer
            Animator.SetLayerWeight(1, 1);
        }

        public void OnStartParkourAction()
        {
            if (firstPersonController != null)
            {
                firstPersonController.Velocity = Vector3.zero;
                firstPersonController.enabled = false;
            }
            if (playerCollider != null) playerCollider.enabled = false;
            if (cameraSwitcher != null) cameraSwitcher.enabled = false;
            if (playerRagdoll != null)
            {
                playerRagdoll.SetActiveRagdollPhysics(true);
            }
            // Disable upper body layer
            Animator.SetLayerWeight(1, 0);
        }

        public IEnumerator HandleVerticalJump()
        {
            if (firstPersonController != null && firstPersonController.isGrounded)
            {
                firstPersonController.DoJump();
                firstPersonController.State = PlayerState.Jumping; // Update state to Jumping
            }
            yield break;
        }

        private void UpdateBodyState()
        {
            switch (firstPersonController.State)
            {
                case PlayerState.Idle:
                    Animator.SetInteger("BodyState", 0);
                    break;
                case PlayerState.Walking:
                    Animator.SetInteger("BodyState", 1);
                    break;
                case PlayerState.Running:
                    Animator.SetInteger("BodyState", 2);
                    break;
                case PlayerState.Crouching:
                    Animator.SetInteger("BodyState", 3);
                    break;
                case PlayerState.Jumping:
                    Animator.SetInteger("BodyState", 4);
                    break;
                case PlayerState.Climbing:
                    Animator.SetInteger("BodyState", 5);
                    break;
                case PlayerState.Sliding:
                    Animator.SetInteger("BodyState", 6);
                    break;
                case PlayerState.Dropping:
                    Animator.SetInteger("BodyState", 7);
                    break;
                case PlayerState.Gliding:
                    Animator.SetInteger("BodyState", 8);
                    break;
                case PlayerState.InVehicle:
                    Animator.SetInteger("BodyState", 9);
                    break;
                case PlayerState.Stealth:
                    Animator.SetInteger("BodyState", 10);
                    break;
                // Add other states as needed
                default:
                    Animator.SetInteger("BodyState", 0);
                    break;
            }

            Debug.Log($"Updated BodyState to {firstPersonController.State} ({Animator.GetInteger("BodyState")})");
        }

        private bool OnEnterPlayerState(PlayerState state)
        {
            return firstPersonController.State == state && lastBodyState != state;
        }
    }
}
